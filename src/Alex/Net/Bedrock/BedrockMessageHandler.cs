using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Alex.Common.Utils;
using Alex.Networking.Bedrock.RakNet;
using Microsoft.IO;
using MiNET.Net;
using MiNET.Net.RakNet;
using MiNET.Utils;
using MiNET.Utils.Cryptography;
using MiNET.Utils.IO;
using NLog;

namespace Alex.Net.Bedrock
{
	public class BedrockMessageHandler : ICustomMessageHandler
	{
		private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

		private readonly McpeClientMessageDispatcher _messageDispatcher;

		public Action ConnectionAction { get; set; }
		public Action<string, bool> DisconnectedAction { get; set; }

		private readonly RaknetSession _session;

		public MiNET.Utils.Cryptography.CryptoContext CryptoContext { get; set; }

		private DateTime _lastPacketReceived;
		public TimeSpan TimeSinceLastPacket => DateTime.UtcNow - _lastPacketReceived;

		private BedrockClientPacketHandler PacketHandler { get; }

		public BedrockMessageHandler(RaknetSession session, BedrockClientPacketHandler handler) : base()
		{
			_session = session;
			_messageDispatcher = new McpeClientMessageDispatcher(handler);
			PacketHandler = handler;
		}

		public void Connected()
		{
			ConnectionAction?.Invoke();
		}

		public void Disconnect(string reason, bool sendDisconnect = true)
		{
			DisconnectedAction?.Invoke(reason, sendDisconnect);
		}

		public List<Packet> PrepareSend(List<Packet> packetsToSend)
		{
			var sendList = new List<Packet>();
			var sendInBatch = new List<Packet>();

			foreach (Packet packet in packetsToSend)
			{
				// We must send forced clear messages in single message batch because
				// we can't mix them with un-encrypted messages for obvious reasons.
				// If need be, we could put these in a batch of it's own, but too rare 
				// to bother.
				if (packet.ForceClear)
				{
					var wrapper = McpeWrapper.CreateObject();
					wrapper.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
					wrapper.ForceClear = true;
					wrapper.payload = Compress(new List<Packet>() { packet });
					//wrapper.Encode(); // prepare
					packet.PutPool();
					sendList.Add(wrapper);

					continue;
				}

				if (packet is McpeWrapper)
				{
					packet.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
					sendList.Add(packet);

					continue;
				}

				if (!packet.IsMcpe)
				{
					packet.ReliabilityHeader.Reliability =
						packet.ReliabilityHeader.Reliability != Reliability.Undefined ?
							packet.ReliabilityHeader.Reliability : Reliability.Reliable;

					sendList.Add(packet);

					continue;
				}

				packet.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;

				sendInBatch.Add(packet);
			}

			if (sendInBatch.Count > 0)
			{
				var batch = McpeWrapper.CreateObject();
				batch.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;

				if (CryptoContext != null && CryptoContext.UseEncryption)
				{
					batch.payload = CryptoUtils.Encrypt(Compress(sendInBatch), CryptoContext);
				}
				else
				{
					batch.payload = Compress(sendInBatch);
				}

				//batch.Encode(); // prepare
				sendList.Add(batch);
			}

			return sendList;
		}

		public static RecyclableMemoryStreamManager MemoryStreamManager { get; set; } =
			new RecyclableMemoryStreamManager();

		private byte[] Compress(ICollection<Packet> packets)
		{
			//  long length = 0;
			//   foreach (Packet packet in packets) length += packet.Encode().Length;

			// var compressionLevel = _session.CompressionThreshold > -1 && length >= _session.CompressionThreshold ?
			//   System.IO.Compression.CompressionLevel.Fastest : System.IO.Compression.CompressionLevel.NoCompression;

			using (MemoryStream stream = MemoryStreamManager.GetStream())
			{
				using (var compressStream = new DeflateStream(stream, CompressionLevel.Fastest, true))
				{
					foreach (Packet packet in packets)
					{
						byte[] bs = packet.Encode();

						if (bs != null && bs.Length > 0)
						{
							VarInt.WriteUInt32(compressStream, (uint)bs.Length);
							//BatchUtils.WriteLength(compressStream, bs.Length);
							compressStream.Write(bs, 0, bs.Length);
						}

						packet.PutPool();
					}

					compressStream.Flush();
				}

				byte[] bytes = stream.ToArray();

				return bytes;
			}
		}

		public Packet HandleOrderedSend(Packet packet)
		{
			if (!packet.ForceClear && CryptoContext != null && CryptoContext.UseEncryption
			    && packet is McpeWrapper wrapper)
			{
				var encryptedWrapper = McpeWrapper.CreateObject();
				encryptedWrapper.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
				encryptedWrapper.payload = CryptoUtils.Encrypt(wrapper.payload, CryptoContext);
				encryptedWrapper.Encode();

				return encryptedWrapper;
			}

			return packet;
		}

		//private object _handlingLock = new object();
		public SemaphoreSlim PacketHandlingSemaphore { get; } = new SemaphoreSlim(1);
		public void HandlePacket(Packet message)
		{
			if (_session.Evicted)
				return;
			
			try
			{
				if (message is McpeWrapper wrapper)
				{
					var payload = wrapper.payload;

					//lock (_handlingLock)
					{
						PacketHandlingSemaphore.Wait();
						try
						{
							// Decrypt byteswrapper
							if (CryptoContext != null && CryptoContext.UseEncryption)
							{
								payload = CryptoUtils.Decrypt(payload, CryptoContext);
							}

							using (var compressionStream = new DeflateStream(
								       new MemoryStreamReader(payload),
								       System.IO.Compression.CompressionMode.Decompress,
								       false))
							{
								payload = compressionStream.ReadToReadOnlyMemory();
							}
						}
						finally
						{
							PacketHandlingSemaphore.Release();
						}
					}

					//var messages = new List<Packet>();

					// Get bytes to process
					//	var payload = wrapper.payload;

					using (var ms = new MemoryStreamReader(payload))
					{
						ms.Position = 0;

						int count = 0;

						// Get actual packet out of bytes
						while (ms.Position < ms.Length)
						{
							if (_session.Evicted)
								return;

							uint len = VarInt.ReadUInt32(ms);
							long pos = ms.Position;

							var data = ms.Read(len);

							ms.Position = pos;
							int id = VarInt.ReadInt32(ms);

							Packet packet = null;

							try
							{
								packet = PacketFactory.Create((byte) id, data, "mcpe") ??
								         new UnknownPacket((byte) id, data);

								packet.ReliabilityHeader = wrapper.ReliabilityHeader;

								//messages.Add(packet);
								count++;
							}
							catch (Exception e)
							{
								Log.Warn(
									e,
									$"Error parsing bedrock message #{count} id={id} (Buffer size={data.Length} Packet size={len})");
							}


							if (packet != null)
							{
								try
								{
									HandleGamePacket(packet);
								}
								catch (Exception e)
								{
									Log.Warn(e, $"Error handling game packet #{count} id={id}");
								}
								finally
								{
									packet.PutPool();
								}
							}

							ms.Position = pos + len;
						}
					}
				}
				else if (message is UnknownPacket unknownPacket)
				{
					Log.Warn(
						$"Received unknown packet 0x{unknownPacket.Id:X2}\n{Packet.HexDump(unknownPacket.Message)}");
				}
				else
				{
					Log.Error(
						$"Unhandled packet: {message.GetType().Name} 0x{message.Id:X2}, IP {_session.EndPoint.Address}");
				}
			}
			finally
			{
				
			}
		}

		private void HandleGamePacket(Packet message)
		{
			if (_session.Evicted)
				return;

			//RaknetSession.TraceReceive(message);

			Stopwatch sw = Stopwatch.StartNew();

			try
			{
				//     Log.Info($"Got packet: {message}");
				if (!_messageDispatcher.HandlePacket(message))
				{
					if (!PacketHandler.HandleOtherPackets(message))
					{
						if (message is UnknownPacket unknownPacket)
						{
							Log.Warn($"Received unknown packet 0x{unknownPacket.Id:X2}\n");
						}
					}
				}
			}
			catch (Exception ex)
			{
				// if (message.Id == 39)
				//     return;
				Log.Warn(ex, $"Packet handling error: {message} - {ex.ToString()}");
			}
			finally
			{
				sw.Stop();

				if (sw.ElapsedMilliseconds > 250)
				{
					Log.Warn(
						$"Packet handling took longer than expected! Time elapsed: {sw.ElapsedMilliseconds}ms (Packet={message})");
				}

				_lastPacketReceived = DateTime.UtcNow;
			}
		}
	}
}
using Alex.Common;
using Alex.Common.Utils.Vectors;
using Alex.Gui.Elements.Context3D;
using Alex.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NLog;
using RocketUI;

namespace Alex.Gui.Elements.Inventory
{
	public class GuiItemRenderer : RocketControl
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(GuiItemRenderer));

		private Item _item = null;

		public Item Item
		{
			get
			{
				return _item;
			}
			set
			{
				var oldItem = _item;
				_item = value;

				ItemChanged(oldItem, value);
			}
		}

		public PlayerLocation EntityPosition { get; set; }
		public GuiContext3DElement.GuiContext3DCamera Camera { get; }
		private RenderTarget2D RenderTarget { get; set; }

		public GuiItemRenderer()
		{
			EntityPosition = new PlayerLocation();
			Camera = new GuiEntityModelView.GuiContext3DCamera(EntityPosition);
		}

		private void ItemChanged(Item old, Item newItem)
		{
			var renderer = newItem.Renderer;

			if (renderer == null || renderer.ResourcePackModel == null)
			{
				Log.Warn($"Could not find renderer for hotbar item: {newItem.Name}");

				return;
			}
			else { }

			//_item.Renderer.Rotation =  new Vector3(0, -90f, 25f);
			//_item.Renderer.Translation = new Vector3(1.13f, 3.2f, 1.13f);
			//_item.Renderer.Scale = new Vector3(0.5f);

			var itemModel = renderer.ResourcePackModel;
			/* if (itemModel.Display.TryGetValue("firstperson_righthand", out var value))
			 {
			     renderer.Rotation = value.Rotation;
			     renderer.Translation = value.Translation;
			     renderer.Scale = value.Scale;
 
			     _item.Renderer = renderer;
			 }*/
		}

		protected override void OnInit(IGuiRenderer renderer)
		{
			RenderTarget = new RenderTarget2D(
				Alex.Instance.GraphicsDevice, 32, 32, false, SurfaceFormat.Color, DepthFormat.None);


			base.OnInit(renderer);
		}

		private Rectangle _previousBounds;

		protected override void OnUpdate(GameTime gameTime)
		{
			base.OnUpdate(gameTime);

			var item = Item;

			if (item == null || item.Renderer == null)
				return;

			var bounds = RootScreen.RenderBounds;

			if (bounds != _previousBounds)
			{
				//var c = bounds.Center;
				//Camera.RenderPosition = new Vector3(c.X, c.Y, 0.0f);
				//Camera.UpdateProjectionMatrix();
				//Camera.UpdateAspectRatio((float) bounds.Width / (float) bounds.Height);
				Camera.UpdateAspectRatio(bounds.Width / (float)bounds.Height);
				_previousBounds = bounds;
			}

			//   Camera.MoveTo(EntityPosition, Vector3.Zero);
			//  Camera.UpdateProjectionMatrix();
			var args = new UpdateArgs()
			{
				GameTime = gameTime, Camera = Camera, GraphicsDevice = Alex.Instance.GraphicsDevice
			};

			Camera.Update(args);
			Camera.UpdateProjectionMatrix();

			item.Renderer.Update(args);
		}

		protected override void OnDraw(GuiSpriteBatch graphics, GameTime gameTime)
		{
			base.OnDraw(graphics, gameTime);

			var item = _item;

			if (item == null || item.Renderer == null)
			{
				return;
			}

			// graphics.Context.GraphicsDevice.SetRenderTarget(RenderTarget);
			// graphics.Context.GraphicsDevice.Clear(Color.White);
			try
			{
				var renderArgs = new RenderArgs()
				{
					GraphicsDevice = graphics.SpriteBatch.GraphicsDevice,
					SpriteBatch = graphics.SpriteBatch,
					GameTime = gameTime,
					Camera = Camera,
				};

				//var viewport = args.Graphics.Viewport;
				//args.Graphics.Viewport = new Viewport(RenderBounds);
				//graphics.End();

				using (var context = graphics.BranchContext(
					       BlendState.AlphaBlend, DepthStencilState.Default, RasterizerState.CullClockwise,
					       SamplerState.PointWrap))
				{
					var bounds = RenderBounds;

					bounds.Inflate(-3, -3);

					var p = graphics.Project(bounds.Location.ToVector2());
					var p2 = graphics.Project(bounds.Location.ToVector2() + bounds.Size.ToVector2());

					var newViewport = Camera.Viewport;
					newViewport.X = (int)p.X;
					newViewport.Y = (int)p.Y;
					newViewport.Width = (int)(p2.X - p.X);
					newViewport.Height = (int)(p2.Y - p.Y);

					Camera.Viewport = newViewport;

					context.Viewport = Camera.Viewport;

					graphics.Begin();

					Item.Renderer.Render(renderArgs, Matrix.Identity);
					// Entity.Render(renderArgs);
					//  EntityModelRenderer?.Render(renderArgs, EntityPosition);

					graphics.End();
				}
			}
			finally
			{
				//   graphics.Context.GraphicsDevice.SetRenderTarget(null);
			}

			// graphics.SpriteBatch.Draw(RenderTarget, new Rectangle(0, 0, 256, 256), Color.White);
		}
	}
}
﻿using System;
using Alex.API.Graphics;
using Alex.API.Graphics.Textures;
using Alex.API.Gui.Graphics;
using Microsoft.Xna.Framework;

namespace Alex.API.Gui.Elements
{
    public class GuiProgressBar : GuiElement
    {

        public int MinValue { get; set; } = 0;
        public int Value { get; set; } = 0;
        public int MaxValue { get; set; } = 100;

        public float Percent => Math.Max(0, Math.Min(1, Value / (float)Math.Abs(MaxValue - MinValue)));

        private int _spriteSheetSegmentWidth = 3;
        public NinePatchTexture2D Highlight { get; set; }
	 //   public SpriteFont FontRenderer { get; set; }

	//	private Vector2 TextSize { get; set; } = Vector2.One;
	 //   private Vector2 TextScale { get; set; } = Vector2.One;
	//	private string Text { get; set; } = string.Empty;
		public GuiProgressBar()
        {
        }

        protected override void OnInit(IGuiRenderer renderer)
        {
	     //   FontRenderer = renderer.DefaultFont;
		//	TextSize = new Vector2(Width, Height);

            var texture = renderer.GetTexture(GuiTextures.ProgressBar);
            var b = texture.ClipBounds;

            _spriteSheetSegmentWidth = (int)Math.Round(b.Width / 4f);
            Background = new NinePatchTexture2D(texture.Texture, new Rectangle(b.X, b.Y, _spriteSheetSegmentWidth * 3, b.Height), _spriteSheetSegmentWidth);
            Highlight = new NinePatchTexture2D(texture.Texture, new Rectangle(_spriteSheetSegmentWidth * 3, b.Y, _spriteSheetSegmentWidth, b.Height), _spriteSheetSegmentWidth);
        }

	    protected override void OnUpdate(GameTime gameTime)
	    {
		    base.OnUpdate(gameTime);

		   /* Text = $"{(int)(Percent * 100)}%";
		    var size = FontRenderer.MeasureString(Text);
		    TextSize = size * TextScale;

		    while (TextSize.Y >= Height)
			{
			    TextScale *= 0.9f;
			    TextSize = size * TextScale;
			}*/
		}

	    protected override void OnDraw(GuiSpriteBatch graphics, GameTime gameTime)
        {
            var bounds = RenderBounds;

            var fillWidth = bounds.Width - 2 * _spriteSheetSegmentWidth;

            base.OnDraw(graphics, gameTime);

            bounds = new Rectangle(bounds.X + _spriteSheetSegmentWidth, bounds.Y, Math.Max(1, (int)(fillWidth * Percent)), bounds.Height);
            graphics.FillRectangle(bounds, Highlight, TextureRepeatMode.Stretch);

	       //	args.SpriteBatch.DrawString(FontRenderer, Text, RenderBounds.Center.ToVector2() - (TextSize / 2f), Color.Black, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
        }
    }
}

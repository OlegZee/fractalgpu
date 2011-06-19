using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OlegZee.FractalBrowser.Fractal
{
	public abstract class LyapRenderer
	{
		public Bitmap Render(Lyapunov.Settings settings)
		{
			var rc = new Rectangle(0, 0, (int)settings.Size.Width, (int)settings.Size.Height);
			var map = RenderImpl(rc.Width, rc.Height, settings.Fractal);

			var bmp = new Bitmap(rc.Width, rc.Height);
			var bmpData = bmp.LockBits(rc, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

			// Using pointer arithmethic to copy all the bits
			var ptr0 = bmpData.Scan0;
			var stride = bmpData.Stride;

			for (var i = 0; i < bmp.Width; i++)
			{
				for (var j = 0; j < bmp.Height; j++)
				{
					var offset = i * 4 + stride * j;
					var clr = ColorFromExp(map[i, rc.Height - j - 1], settings.Contrast).ToArgb();
					Marshal.WriteInt32(ptr0, offset, clr);
				}
			}

			bmp.UnlockBits(bmpData);
			return bmp;
		}

		private static Color ColorFromExp(double lyapExp, double contrast)
		{
			int colorIntensity;
			if (lyapExp > 0)
			{
				colorIntensity = (int)(Math.Exp(-lyapExp) * 255);
				return Color.FromArgb(255, 0, 0, 255 - colorIntensity);
			}
			if (Double.IsNegativeInfinity(lyapExp))
			{
				return Color.Black;
			}
			if (Double.IsNaN(lyapExp))
			{
				return Color.White;
			}

			colorIntensity = (int)(Math.Exp(lyapExp) * 255);
			colorIntensity = (int)(Math.Pow(colorIntensity, contrast) / Math.Pow(255, (contrast - 1)));

			return Color.FromArgb(255, colorIntensity, (int)(colorIntensity * .85), 0);
		}

		public abstract double[,] RenderImpl(int w, int h, Lyapunov.FractalSettings settings);
	}
}
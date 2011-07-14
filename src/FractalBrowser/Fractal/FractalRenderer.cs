using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OlegZee.FractalBrowser.Fractal
{
	public abstract class FractalRenderer<TSettings> where TSettings:RenderSettings
	{
		public abstract Bitmap Render(TSettings settings);
		public abstract float[,] RenderImpl(int w, int h, TSettings settings);

		protected Bitmap Render(TSettings settings, Func<float, Color> mapColor)
		{
			var rc = new Rectangle(0, 0, (int)settings.Size.Width, (int)settings.Size.Height);
			var map = RenderImpl(rc.Width, rc.Height, settings);

			return CreateBitmap(map, mapColor);
		}

		protected static Bitmap CreateBitmap(float[,] map, Func<float, Color> mapColor)
		{
			var width = map.GetLength(0);
			var height = map.GetLength(1);

			var bmp = new Bitmap(width, height);
			var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

			// Using pointer arithmethic to copy all the bits
			var ptr0 = bmpData.Scan0;
			var stride = bmpData.Stride;

			for (var i = 0; i < width; i++)
			{
				for (var j = 0; j < height; j++)
				{
					var offset = i * 4 + stride * j;
					var clr = mapColor(map[i, height - j - 1]).ToArgb();
					Marshal.WriteInt32(ptr0, offset, clr);
				}
			}

			bmp.UnlockBits(bmpData);
			return bmp;
		}

	}
}
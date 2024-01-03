using System.Drawing;
using FractalGpu.RenderCli.Media;

namespace FractalGpu.RenderCli.Fractal
{
	public abstract class FractalRenderer<TSettings> where TSettings:RenderSettings
	{
		public abstract RawBitmap Render(TSettings settings);
		public abstract float[,] RenderImpl(int w, int h, TSettings settings);

		protected RawBitmap Render(TSettings settings, Func<float, RawColor> mapColor)
		{
			var rc = new Rectangle(0, 0, (int)settings.Size.Width, (int)settings.Size.Height);
			var map = RenderImpl(rc.Width, rc.Height, settings);

			return CreateBitmap(map, mapColor);
		}

		protected static RawBitmap CreateBitmap(float[,] map, Func<float, RawColor> mapColor)
		{
			var width = map.GetLength(0);
			var height = map.GetLength(1);

			var bmp = new RawBitmap(width, height);
			// var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

			// Using pointer arithmetic to copy all the bits
			// var ptr0 = bmpData.Scan0;
			// var stride = bmpData.Stride;

			for (var i = 0; i < width; i++)
			for (var j = 0; j < height; j++)
			{
				// var offset = i * 4 + stride * j;
				var clr = mapColor(map[i, height - j - 1]);
				// Marshal.WriteInt32(ptr0, offset, clr);
				bmp.SetPixel(i, j, clr);
			}

			// bmp.UnlockBits(bmpData);
			return bmp;
		}

	}
}
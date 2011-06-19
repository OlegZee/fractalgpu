using System;
using System.Linq;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// CPU based renderer implementation
	/// </summary>
	public class LyapRendererCpu : LyapRenderer
	{
		protected const double Contrast = 1.7;

		public override double[,] RenderImpl(int w, int h, Lyapunov.FractalSettings settings)
		{
			var result = new double[w,h];
			var ascale = (settings.A.End - settings.A.Start)/w;
			var bscale = (settings.B.End - settings.B.Start)/h;

			for (var i = 0; i < w; i++)
			{
				for (var j = 0; j < h; j++)
				{
					var a = settings.A.Start + i * ascale;
					var b = settings.B.Start + j * bscale;
					var pattern = settings.Pattern.ToCharArray().Select(c => c == 'a' ? a : b).ToArray();

					result[i, j] = GetPixelColor(Contrast, pattern, settings.InitialValue, settings.Warmup, settings.Iterations);
				}
			}

			return result;
		}

		protected static double GetPixelColor(double contrast, double[] pattern, double initial, int warmup, int iterations)
		{
			double total = 0; //For Lyap exp. determination
			var x = initial;
			var patternSize = pattern.Length;

			for (var i = 0; i < iterations; i++)
			{
				var r = pattern[i%patternSize];
				x *= r*(1 - x);

				var d = Math.Log(Math.Abs(r - 2*r*x));

				if (i < warmup) continue;

				total += d;
				if (Double.IsNaN(d) || Double.IsNegativeInfinity(d) || Double.IsPositiveInfinity(d))
				{
					break;
				}
			}

			return total/Math.Log(2)/(iterations - warmup);
		}
	}
}
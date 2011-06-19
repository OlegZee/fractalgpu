using System;
using System.Drawing;
using System.Linq;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// CPU based renderer implementation
	/// </summary>
	public class LyapRendererCpu : FractalRenderer<Lyapunov.Settings>
	{
		protected const double Contrast = 1.7;

		public override Bitmap Render(Lyapunov.Settings settings)
		{
			return Render(settings, d => ColorFromExp(d, settings.Contrast));
		}

		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
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

					result[i, j] = CalculateExponent(pattern, settings.InitialValue, settings.Warmup, settings.Iterations);
				}
			}

			return result;
		}

		#region Implementation

		private static Color ColorFromExp(double lyapExp, double contrast)
		{
			int colorIntensity;
			if (Double.IsNegativeInfinity(lyapExp) || Double.IsPositiveInfinity(lyapExp))
			{
				return Color.Black;
			}
			if (Double.IsNaN(lyapExp))
			{
				return Color.White;
			}
			if (lyapExp > 0)
			{
				colorIntensity = (int)(Math.Exp(-lyapExp) * 255);
				return Color.FromArgb(255, 0, 0, 255 - colorIntensity);
			}

			colorIntensity = (int)(Math.Exp(lyapExp) * 255);
			colorIntensity = (int)(Math.Pow(colorIntensity, contrast) / Math.Pow(255, (contrast - 1)));

			if (colorIntensity > 255)
				colorIntensity = 255;
			else if (colorIntensity < 0)
				colorIntensity = 0;

			return Color.FromArgb(255, colorIntensity, (int)(colorIntensity * .85), 0);
		}

		protected static double CalculateExponent(double[] pattern, double initial, int warmup, int iterations)
		{
			var x = initial;
			var patternSize = pattern.Length;

			for (var i = 0; i < warmup; i++)
			{
				var r = pattern[i%patternSize];
				x *= r*(1 - x);
			}

			double total = 0; //For Lyap exp. determination
			for (var i = warmup; i < iterations; i++)
			{
				var r = pattern[i%patternSize];
				x *= r*(1 - x);

				var d = Math.Log(Math.Abs(r - 2*r*x));

				total += d;
				if (Double.IsNaN(d) || Double.IsNegativeInfinity(d) || Double.IsPositiveInfinity(d))
				{
					return d;
				}
			}

			return total/Math.Log(2)/(iterations - warmup);
		}

		#endregion
	}
}
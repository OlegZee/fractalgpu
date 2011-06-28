﻿using System;
using System.Linq;

namespace OlegZee.FractalBrowser.Fractal
{
	internal class LyapRendererCpu : LyapRendererBase
	{
		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var result = new double[w, h];
			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			var pattern = new double[settings.Pattern.Length];

			for (var i = 0; i < w; i++)
			{
				for (var j = 0; j < h; j++)
				{
					var b = settings.B.Start + i * bscale;
					var a = settings.A.Start + j * ascale;

					for (var k = 0; k < settings.Pattern.Length; k++)
					{
						pattern[k] = settings.Pattern[k] == 'a' ? a : b;
					}

					result[i, j] = CalculateExponent(pattern, settings.InitialValue, settings.Warmup, settings.Iterations);
				}
			}

			return result;
		}

		protected static double CalculateExponent(double[] pattern, double initial, int warmup, int iterations)
		{
			var x = initial;
			var patternSize = pattern.Length;

			for (var i = 0; i < warmup; i++)
			{
				var r = pattern[i % patternSize];
				x *= r * (1 - x);
			}

			double total = 0; //For Lyap exp. determination
			for (var i = warmup; i < iterations; i++)
			{
				var r = pattern[i % patternSize];

				var d = Math.Log(Math.Abs(r - 2 * r * x));

				total += d;
				if (Double.IsNaN(d) || Double.IsNegativeInfinity(d) || Double.IsPositiveInfinity(d))
				{
					return d;
				}

				x *= r * (1 - x);
			}

			return total / Math.Log(2) / (iterations - warmup);
		}
	}
}
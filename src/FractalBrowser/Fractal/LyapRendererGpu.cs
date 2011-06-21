using Microsoft.ParallelArrays;

namespace OlegZee.FractalBrowser.Fractal
{
	using FPA = FloatParallelArray;
	using Math = ParallelArrays;

	/// <summary>
	/// Highly parallel GPU fractal renderer implementation
	/// </summary>
	internal class LyapRendererGpu : LyapRendererBase
	{
		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			// TODO check ShiftDefault, Replicate and such
			var dimensions = new [] {w, h};
			var aArray = new float[w,h];
			var bArray = new float[w,h];

			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			for (var i = 0; i < w; i++)
			for (var j = 0; j < h; j++)
			{
				var vb = settings.B.Start + i * bscale;
				var va = settings.A.Start + j * ascale;

				aArray[i, j] = (float) va;
				bArray[i, j] = (float) vb;
			}

			var x = new FPA((float)settings.InitialValue, dimensions);

			var a = new FPA(aArray);
			var b = new FPA(bArray);

			// warmup cycle, no limit calculation
			for (var i = 0; i < settings.Warmup; i++)
			{
				var r = settings.Pattern[i % settings.Pattern.Length] == 'a' ? a : b;
				x *= r * (1.0f - x);
			}

			var total = new FPA(0, dimensions);
			for (var i = settings.Warmup; i < settings.Iterations; i++)
			{
				var r = settings.Pattern[i % settings.Pattern.Length] == 'a' ? a : b;
				x *= r * (1.0f - x);
				var d = Math.Log2(Math.Abs(r - 2 * r * x));

				total += d;
			}

			total *= 1f/(settings.Iterations - settings.Warmup);

			using (var dx9Targ = new DX9Target())
			{
				var resultBuffer = dx9Targ.ToArray2D(total);
				var result = new double[w,h];

				for (var i = 0; i < w; i++)
					for (var j = 0; j < h; j++)
					{
						result[i, j] = resultBuffer[i, j];
					}
				return result;
			}
		}
	}
}

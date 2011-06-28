using System;
using System.Linq;
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
			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			var aArray = Enumerable.Range(0, h).Select(i => (float) (settings.A.Start + i*ascale)).ToArray();
			var bArray = Enumerable.Range(0, w).Select(i => (float) (settings.B.Start + i*bscale)).ToArray();

			var total = Calculate(aArray, bArray, settings);

			var target = new double[w,h];

			using (var dx9Targ = new DX9Target())
			{
				float[,] resultBuffer;
				var asyncResult = dx9Targ.BeginToArray(total, out resultBuffer);

				// for some reason this async call works much more stable than ToArray2D
				asyncResult.AsyncWaitHandle.WaitOne();

				CopyToDouble(resultBuffer, target, 0, 0);
			}

			return target;
		}

		private static FPA Calculate(float[] aData, float[] bData, Lyapunov.Settings settings)
		{
			var w = bData.Length;
			var h = aData.Length;

			var dimensions = new[] { w, h };
			var aArray = new float[1, h];
			var bArray = new float[w, 1];

			for (var i = 0; i < w; i++) bArray[i, 0] = bData[i];
			for (var j = 0; j < h; j++) aArray[0, j] = aData[j];

			var x = new FPA((float)settings.InitialValue, dimensions);

			// creating A,B arrays on the fly a few times faster!
			var fr = new Func<int, FPA>(i => Math.Replicate(new FPA(
				settings.Pattern[i % settings.Pattern.Length] == 'a' ? aArray : bArray), h, w));

			// warmup cycle, no limit calculation
			for (var i = 0; i < settings.Warmup; i++)
			{
				var r = fr(i);
				x *= r * (1.0f - x);
			}

			var total = new FPA(0, dimensions);
			for (var i = settings.Warmup; i < settings.Iterations; i++)
			{
				var r = fr(i);

				total += Math.Log2(Math.Abs(r - 2 * r * x));
				x *= r - r * x;
			}

			total *= 1f / (settings.Iterations - settings.Warmup);

			return total;
		}

		private static void CopyToDouble(float[,] array, double[,] target, int off1, int off2)
		{
			var w = array.GetUpperBound(0);
			var h = array.GetUpperBound(1);

			for (var i = 0; i < w; i++)
			for (var j = 0; j < h; j++)
			{
				target[i + off1, j + off2] = array[i, j];
			}
		}
	}
}

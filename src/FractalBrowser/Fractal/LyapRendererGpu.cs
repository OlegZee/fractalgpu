using System;
using System.Linq;
using System.Threading;
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
			var dimensions = new [] {w, h};
			var aArray = new float[1,w];
			var bArray = new float[h,1];

			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			for (var i = 0; i < w; i++) bArray[i,0] = (float) (settings.B.Start + i * bscale);
			for (var j = 0; j < h; j++) aArray[0,j] = (float) (settings.A.Start + j*ascale);

			var x = new FPA((float)settings.InitialValue, dimensions);

			// creating A,B arrays on the fly a few times faster!
			var a = new Func<FPA>(() => Math.Replicate(new FPA(aArray), h, w));
			var b = new Func<FPA>(() => Math.Replicate(new FPA(bArray), h, w));
			var R = new Func<int, FPA>(i => settings.Pattern[i%settings.Pattern.Length] == 'a' ? a() : b());

			// warmup cycle, no limit calculation
			for (var i = 0; i < settings.Warmup; i++)
			{
				var r = R(i);
				x *= r * (1.0f - x);
			}

			var total = new FPA(0, dimensions);
			for (var i = settings.Warmup; i < settings.Iterations; i++)
			{
				var r = R(i);

				var d = Math.Log2(Math.Abs(r - 2 * r * x));
				total += d;

				x *= r - r * x;
			}

			total *= 1f/(settings.Iterations - settings.Warmup);

			using (var dx9Targ = new DX9Target())
			{
				float[,] resultBuffer;
				var asyncResult = dx9Targ.BeginToArray(total, out resultBuffer);

				// for some reason this async call works much more stable than ToArray2D
				asyncResult.AsyncWaitHandle.WaitOne();

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

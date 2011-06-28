using System;
using System.Collections.Generic;
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
			int splitX, splitY;
			CalculateSplits(w, h, out splitX, out splitY, settings.Iterations);

			if(w % splitX != 0 || h % splitY != 0)
				throw new ArgumentException(string.Format("w and h should be multiply of {0} and {1} respectively", splitX, splitY));

			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			var actions = new List<Action>();
			var handles = new List<WaitHandle>();

			var target = new double[w, h];

			using (var dx9Targ = new DX9Target())
			{
				for (var sx = 0; sx < splitX; sx++)
				for (var sy = 0; sy < splitY; sy++)
				{
					var offsetI = w / splitX * sx;
					var offsetJ = h / splitY * sy;

					var aArray = Enumerable.Range(0, h/splitY).Select(j => (float) (settings.A.Start + (offsetJ + j)*ascale)).ToArray();
					var bArray = Enumerable.Range(0, w/splitX).Select(i => (float) (settings.B.Start + (offsetI + i)*bscale)).ToArray();

					var total = Calculate(aArray, bArray, settings);

					float[,] resultBuffer;
					var asyncResult = dx9Targ.BeginToArray(total, out resultBuffer);
					handles.Add(asyncResult.AsyncWaitHandle);

					actions.Add(() => CopyToDouble(resultBuffer, target, offsetI, offsetJ));
				}

				// for some reason this async call works much more stable than ToArray2D
				handles.All(handle => handle.WaitOne());
				foreach (var a in actions)
				{
					a();
				}
			}

			return target;
		}

		private static void CalculateSplits(int w, int h, out int splitX, out int splitY, int iterationCount)
		{
			splitX = splitY = 1;

			while (w / splitX > 1024)
			{
				splitX *= 2;
			}
			while (h / splitY > 1024)
			{
				splitY *= 2;
			}

			var resultingResolution = h/splitY*w/splitX * 1f;

			for (var m = 0; m < 10; m++ )
			{
				if (resultingResolution * iterationCount < 0.9e9f) break;

				if (w / splitX > h / splitY)
					splitX *= 2;
				else
					splitY *= 2;
				resultingResolution /= 2;
			}
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
				settings.Pattern[i % settings.Pattern.Length] == 'a' ? aArray : bArray), w, h));

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
			var w = array.GetLength(0);
			var h = array.GetLength(1);

			for (var i = 0; i < w; i++)
			for (var j = 0; j < h; j++)
			{
				target[i + off1, j + off2] = array[i, j];
			}
		}
	}
}

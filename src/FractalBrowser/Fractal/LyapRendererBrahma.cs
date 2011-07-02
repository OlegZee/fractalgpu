#if false
/*
 I did implemented it and tested under VS2010 targeting .NET 3.5 it fails to compile.
 Brahma is bound to linq expressions representation which differs on .NET 3.5 and 4.0.
 */

using System;
using System.Linq;
using Brahma;
using Brahma.Types;

namespace OlegZee.FractalBrowser.Fractal
{
	using Brahma.OpenCL;
	using Math = Brahma.Math;

	/// <summary>
	/// Fractal renderer implementation using Brahma GPU library
	/// </summary>
	internal class LyapRendererBrahma : LyapRendererBase
	{
		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			var aValues = Enumerable.Range(0, h).Select(j => (float)(settings.A.Start + (j) * ascale)).ToArray();
			var bValues = Enumerable.Range(0, w).Select(i => (float)(settings.B.Start + (i) * bscale)).ToArray();

			// a -> 0
			var mask = settings.Pattern.ToCharArray().Select(c => c == 'a' ? 0 : 1).ToArray();

			// Create a data-parallel array and fill it with data
			using(var engine = ComputeProvider.Create("*", OpenCL.Net.Cl.DeviceType.Gpu))
			using(var aData = new Buffer<float32>(engine, Operations.ReadOnly, Memory.Device, aValues))
			using(var bData = new Buffer<float32>(engine, Operations.ReadOnly, Memory.Device, bValues))
			using (var maskData = new Buffer<float32>(engine, Operations.ReadOnly, Memory.Device, mask))
			{
				var totalValues = new float[bValues.Length*aValues.Length];
				var totalData = new Buffer<float32>(engine, Operations.ReadWrite, Memory.Device, totalValues);

				// Compile the query
				var query = Kernel(engine, settings.Warmup, settings.Iterations,
					bValues.Length, mask.Length, (float) settings.InitialValue);

				// Run the query on this data
				var result = query.Run(new _2D(aData.Length, bData.Length, 1, 1), aData, bData, maskData, totalData);

				var commandQueue = new CommandQueue(engine, engine.Devices.First(), false);
				commandQueue.Add(result).Finish();
				commandQueue.Add(totalData.Read(0, bData.Length*aData.Length, totalValues)).Finish();

				var target = new double[w, h];
				for (var i = 0; i < w; i++)
				for (var j = 0; j < h; j++)
				{
					target[i, j] = totalValues[i + j*w];
				}

				return target;
			}
		}

		private static Kernel<_2D, Buffer<float32>, Buffer<float32>, Buffer<float32>, Buffer<float32>> Kernel(
			ComputeProvider engine, int p_warmupCount, int p_iterationsCount, int p_columns, int p_masklen, float p_initialX)
		{
			int32 iterationsCount = p_iterationsCount, warmupCount = p_warmupCount;
			float32 divider = 1f / (iterationsCount - warmupCount);
			float32 initialX = p_initialX;
			int32 columns = p_columns;
			int32 maskLen = p_masklen;

			const CompileOptions compileOptions = CompileOptions.UseNativeFunctions | CompileOptions.FusedMultiplyAdd | CompileOptions.FastRelaxedMath;

			var query = engine.Compile<_2D, Buffer<float32>, Buffer<float32>, Buffer<float32>, Buffer<float32>>
				(
					(ds, a, b, m, t) =>
						from pt in ds
						let i = pt.GlobalID0
						let j = pt.GlobalID1
						let x = initialX
						let r = default(float32)
						let bv = b[i]
						let av = a[j]
						let warmup = engine.Loop(0, warmupCount, idxs =>
							idxs.Select(idx =>
								new Set[]
									{
										r <= (m[idx%maskLen] == 0 ? av : bv),
										x <= r*x - r*x*x
									})
							)
						let total = default(float32)
						let calculate = engine.Loop(warmupCount, iterationsCount, idxs =>
							idxs.Select(idx =>
								new Set[]
									{
										r <= (m[idx%maskLen] == 0 ? av : bv),
										total <= total + Math.Log(Math.Fabs(r - 2*r*x)),
										x <= r*x - r*x*x
									})
							)
						select new Set[]
							{
								t[j*columns + i] <= total*divider
							}
				, compileOptions);
			return query;
		}

	}
}

#endif
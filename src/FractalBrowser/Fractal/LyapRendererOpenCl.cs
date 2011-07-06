using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cloo;
using OlegZee.FractalBrowser.Properties;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Fractal renderer implementation using Brahma GPU library
	/// </summary>
	internal class LyapRendererOpenCl : LyapRendererBase
	{
		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var bscale = (settings.B.End - settings.B.Start)/w;
			var ascale = (settings.A.End - settings.A.Start)/h;

			var aValues = Enumerable.Range(0, h).Select(j => (float) (settings.A.Start + (j)*ascale)).ToArray();
			var bValuesRaw = Enumerable.Range(0, w).Select(i => (float) (settings.B.Start + (i)*bscale)).ToArray();

			var mask = settings.Pattern.ToCharArray().Select(c => c == 'a' ? 0 : 1).ToArray();

			var platform = ComputePlatform.Platforms[0];
			Debug.WriteLine(string.Format("Using platform {0}, devices: {1}", platform.Name,
				string.Join(", ", platform.Devices.Select(device => device.Name).ToArray()))
				);

			var wsplit = 1;
			// empirical rule to split the data for better performance
			while(w * h / wsplit > 2<<19)
			{
				wsplit *= 2;
			}

			var partLength = bValuesRaw.Length/wsplit;

			var resultData = new float[wsplit][];
			const ComputeMemoryFlags roBufferFlags = ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer;

			var properties = new ComputeContextPropertyList(platform);
			using (var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero))
			using (var aData = new ComputeBuffer<float>(context, roBufferFlags, aValues))
			using (var maskData = new ComputeBuffer<int>(context, roBufferFlags, mask))
			using (var program = new ComputeProgram(context, Resources.Lyapunov))
			{
				program.Build(null, null, null, IntPtr.Zero);
				var eventList = new ComputeEventList();
				var commands = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);

				var kernelFunction = program.CreateKernel("Lyapunov");
				var disposables = new List<IDisposable>();

				for (var part = 0; part < wsplit; part++)
				{
					var bData = new ComputeBuffer<float>(context, roBufferFlags, bValuesRaw.Skip(part*partLength).Take(partLength).ToArray());
					var resultBuffer = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, partLength*aValues.Length);

					resultData[part] = new float[partLength*aValues.Length];

					kernelFunction.SetMemoryArgument(0, aData);
					kernelFunction.SetMemoryArgument(1, bData);
					kernelFunction.SetMemoryArgument(2, maskData);
					kernelFunction.SetMemoryArgument(3, resultBuffer);
					kernelFunction.SetValueArgument(4, (float) settings.InitialValue);
					kernelFunction.SetValueArgument(5, settings.Warmup);
					kernelFunction.SetValueArgument(6, settings.Iterations);
					kernelFunction.SetValueArgument(7, mask.Length);
					kernelFunction.SetValueArgument(8, (int)bData.Count);
					kernelFunction.SetValueArgument(9, 1f/(settings.Iterations - settings.Warmup));

					commands.Execute(kernelFunction, null, new long[] {bData.Count, aValues.Length}, null, eventList);
					commands.ReadFromBuffer(resultBuffer, ref resultData[part], false, eventList);

					disposables.AddRange(new[]{bData, resultBuffer});
				}
				commands.Finish();

				disposables.Reverse();
				foreach (var disposable in disposables)
				{
					disposable.Dispose();
				}
			}

			var target = new double[w,h];
			for (var s = 0; s < wsplit; s++)
			{
				var chunk = resultData[s];
				var cx = s*partLength;

				for (var i = 0; i < partLength; i++)
				{
					for (var j = 0; j < h; j++)
					{
						target[cx + i, j] = chunk[i + j*partLength];
					}
				}
			}

			return target;
		}

	}
}

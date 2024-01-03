using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cloo;
using OlegZee.Fractal;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Fractal renderer implementation using Brahma GPU library
	/// </summary>
	internal class LyapRendererOpenCl : LyapRendererBase
	{
		public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var bscale = (settings.B.End - settings.B.Start)/w;
			var ascale = (settings.A.End - settings.A.Start)/h;

			var aValuesRaw = Enumerable.Range(0, h).Select(j => (float) (settings.A.Start + (j)*ascale)).ToArray();
			var bValuesRaw = Enumerable.Range(0, w).Select(i => (float) (settings.B.Start + (i)*bscale)).ToArray();

			var mask = settings.Pattern.Select(c => c == 'a' ? 0 : 1).ToArray();

			var platform = ComputePlatform.Platforms[0];
			var device = platform.Devices[0];
			Debug.WriteLine("Using platform {0}, device: {1}", platform.Name, device.Name);

			var hsplit = 1;
			// empirical rule to split the data for better performance
			while(w * h / hsplit > 2<<20)
			{
				hsplit *= 2;
			}

			var chunkLen = aValuesRaw.Length / hsplit;

			var resultData = new float[hsplit][];
			const ComputeMemoryFlags roBufferFlags = ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer;

			var properties = new ComputeContextPropertyList(platform);
			using (var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero))
			using (var bData = new ComputeBuffer<float>(context, roBufferFlags, bValuesRaw))
			using (var maskData = new ComputeBuffer<int>(context, roBufferFlags, mask))
			using (var program = new ComputeProgram(context, Resources.Lyapunov))
			using (var commands = new ComputeCommandQueue(context, device, ComputeCommandQueueFlags.None))
			{
				var disposables = new List<IDisposable>();

				try
				{
					program.Build(new List<ComputeDevice> { device }, null, null, IntPtr.Zero);
				}
				catch (Exception e)
				{
					Console.WriteLine(program.GetBuildLog(device));
				}
				
				var eventList = new ComputeEventList();
				var kernelFunction = program.CreateKernel("Lyapunov");

				disposables.Add(kernelFunction);

				var aPartitions = aValuesRaw.Chunk(chunkLen).ToList();
				for (var chunkIndex = 0; chunkIndex < hsplit; chunkIndex++)
				{
					var aData = new ComputeBuffer<float>(context, roBufferFlags, aPartitions[chunkIndex]);
					var resultBuffer = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, chunkLen * bValuesRaw.Length);
					disposables.AddRange(new[]{aData, resultBuffer});

					resultData[chunkIndex] = new float[chunkLen * bValuesRaw.Length];

					kernelFunction.SetMemoryArgument(0, bData);
					kernelFunction.SetMemoryArgument(1, aData);
					kernelFunction.SetMemoryArgument(2, resultBuffer);
					kernelFunction.SetMemoryArgument(3, maskData);
					kernelFunction.SetValueArgument(4, (float) settings.InitialValue);
					kernelFunction.SetValueArgument(5, settings.Warmup);
					kernelFunction.SetValueArgument(6, settings.Iterations);
					kernelFunction.SetValueArgument(7, mask.Length);
					kernelFunction.SetValueArgument(8, 1f/(settings.Iterations - settings.Warmup));

					commands.Execute(kernelFunction, null, new long[] { bValuesRaw.Length/4, chunkLen }, null, eventList);
					commands.ReadFromBuffer(resultBuffer, ref resultData[chunkIndex], false, eventList);
				}
				commands.Finish();

				disposables.Reverse();
				foreach (var disposable in disposables)
				{
					disposable.Dispose();
				}
			}

			var target = new float[w, h];

			for (var chIdx = 0; chIdx < hsplit; chIdx++)
			{
				var chunk = resultData[chIdx];
				var rowOffset = chIdx * chunkLen;
			
				for (var j = 0; j < chunkLen; j++)
				for (var i = 0; i < w; i++)
				{
					target[i, rowOffset + j] = chunk[i + j * w];
				}
			}

			return target;
		}

	}
}

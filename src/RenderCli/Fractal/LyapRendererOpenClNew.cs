using System.Diagnostics;
using Cloo;
using OlegZee.Fractal;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Fractal renderer implementation using Brahma GPU library
	/// </summary>
	internal class LyapRendererOpenClNew : LyapRendererBase
	{
		public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			if (ComputePlatform.Platforms.Count == 0)
			{
				throw new Exception("No compute platforms available");
			}
			
			var bscale = (settings.B.End - settings.B.Start)/w;
			var ascale = (settings.A.End - settings.A.Start)/h;

			var aValuesRaw = Enumerable.Range(0, h).Select(j => (float) (settings.A.Start + (j)*ascale)).ToArray();
			var bValuesRaw = Enumerable.Range(0, w).Select(i => (float) (settings.B.Start + (i)*bscale)).ToArray();

			var mask = settings.Pattern.ToCharArray().Select(c => c == 'a' ? 0 : 1).ToArray();
			float[] resultData = new float[aValuesRaw.Length*bValuesRaw.Length];

			var platform = ComputePlatform.Platforms[0];
			var device = platform.Devices[0];

			Console.WriteLine("MaxWorkItemSizes: {0}",
				string.Join(' ', device.MaxWorkItemSizes));

			const ComputeMemoryFlags roBufferFlags = ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer;

			var properties = new ComputeContextPropertyList(platform);
			using (var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero))
			using (var bData = new ComputeBuffer<float>(context, roBufferFlags, bValuesRaw))
			using (var maskData = new ComputeBuffer<int>(context, roBufferFlags, mask))
			using (var program = new ComputeProgram(context, Resources.LyapunovNonoptimized))
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

				var kernelFunction = program.CreateKernel("LyapunovSimple");

				disposables.Add(kernelFunction);

				var aData = new ComputeBuffer<float>(context, roBufferFlags, aValuesRaw);
				var resultBuffer = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, aValuesRaw.Length * bValuesRaw.Length);
				disposables.AddRange(new[]{aData, resultBuffer});

				kernelFunction.SetMemoryArgument(0, aData);
				kernelFunction.SetMemoryArgument(1, bData);
				kernelFunction.SetMemoryArgument(2, maskData);
				kernelFunction.SetMemoryArgument(3, resultBuffer);
				kernelFunction.SetValueArgument(4, (float) settings.InitialValue);
				kernelFunction.SetValueArgument(5, settings.Warmup);
				kernelFunction.SetValueArgument(6, settings.Iterations);
				kernelFunction.SetValueArgument(7, mask.Length);
				kernelFunction.SetValueArgument(8, 1f/(settings.Iterations - settings.Warmup));
				kernelFunction.SetValueArgument(9, aValuesRaw.Length);

				commands.Execute(kernelFunction, null, new [] { aData.Count, bData.Count }, null, eventList);
				commands.Finish();
				commands.ReadFromBuffer(resultBuffer, ref resultData, false, eventList);
				commands.Finish();
				
				disposables.Reverse();
				foreach (var disposable in disposables)
				{
					disposable.Dispose();
				}
			}

			var target = new float[w, h];
			for (var j = 0; j < h; j++)
			for (var i = 0; i < w; i++)
			{
				target[i, j] = resultData[i + j * w];
			}

			// Console.WriteLine(string.Join('\n',
			// 	from row in resultData.Chunk(w)
			// 	select string.Join(' ', Array.ConvertAll(row, f => $"{f:f2} "))
			// ));

			return target;
		}

	}
}

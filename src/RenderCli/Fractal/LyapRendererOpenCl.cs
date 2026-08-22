using System.Diagnostics;
using Cloo;

namespace FractalGpu.RenderCli.Fractal
{
	/// <summary>
	/// Fractal renderer implementation on OpenCL/Cloo.
	/// </summary>
	internal class LyapRendererOpenCl : LyapRendererBase
	{
		private readonly ComputePlatform _platform;
		private readonly ComputeDevice _device;

		public LyapRendererOpenCl(int deviceIndex = 0)
		{
			var devices = OpenClDevices.Enumerate();
			foreach (var (p, d) in devices)
			{
				Trace.WriteLine($"Platform: {p.Name}, device: {d.Name}");
			}
			Trace.WriteLine(devices.Count == 0 ? "NO DEVICES FOUND" : $"Devices list is OK");

			(_platform, _device) = OpenClDevices.GetByIndex(deviceIndex);
		}

		public override string ToString() => $"{nameof(LyapRendererOpenCl)}[{_device.Name}]";
		
		public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var bscale = (settings.B.End - settings.B.Start)/w;
			var ascale = (settings.A.End - settings.A.Start)/h;

			var aValuesRaw = Enumerable.Range(0, h).Select(j => (float) (settings.A.Start + (j)*ascale)).ToArray();
			var bValuesRaw = Enumerable.Range(0, w).Select(i => (float) (settings.B.Start + (i)*bscale)).ToArray();

			var mask = settings.Pattern.Select(c => c == 'a' ? 0 : 1).ToArray();

			var platform = _platform;
			var device = _device;
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
			using (var context = new ComputeContext(new List<ComputeDevice> { device }, properties, null, IntPtr.Zero))
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
				catch (Exception)
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

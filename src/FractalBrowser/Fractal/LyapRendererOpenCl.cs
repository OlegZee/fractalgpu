using System;
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
			var bscale = (settings.B.End - settings.B.Start) / w;
			var ascale = (settings.A.End - settings.A.Start) / h;

			var aValues = Enumerable.Range(0, h).Select(j => (float)(settings.A.Start + (j) * ascale)).ToArray();
			var bValues = Enumerable.Range(0, w).Select(i => (float)(settings.B.Start + (i) * bscale)).ToArray();

			var mask = settings.Pattern.ToCharArray().Select(c => c == 'a' ? 0 : 1).ToArray();

			var platform = ComputePlatform.Platforms[0];
			Debug.WriteLine(string.Format("Using platform {0}, devices: {1}", platform.Name,
				string.Join(", ", platform.Devices.Select(device => device.Name).ToArray()))
			);

            var properties = new ComputeContextPropertyList(platform);
            using(var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero))

			// create data buffers
			using(var aData = new ComputeBuffer<float>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, aValues))
			using(var bData = new ComputeBuffer<float>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, bValues))
			using (var maskData = new ComputeBuffer<int>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, mask))
			using (var totalData = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, bValues.Length * aValues.Length))
			using (var program = new ComputeProgram(context, Resources.Lyapunov))
			{
				program.Build(null, null, null, IntPtr.Zero);

				var kernel = program.CreateKernel("Lyapunov");
				kernel.SetMemoryArgument(0, aData);
				kernel.SetMemoryArgument(1, bData);
				kernel.SetMemoryArgument(2, maskData);
				kernel.SetMemoryArgument(3, totalData);
				kernel.SetValueArgument(4, (float) settings.InitialValue);
				kernel.SetValueArgument(5, settings.Warmup);
				kernel.SetValueArgument(6, settings.Iterations);
				kernel.SetValueArgument(7, mask.Length);

				var eventList = new ComputeEventList();
				var commands = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);

				commands.Execute(kernel, null, new long[] {aValues.Length, bValues.Length}, null, eventList);

				var totalValues = new float[bValues.Length * aValues.Length];
				commands.ReadFromBuffer(totalData, ref totalValues, false, eventList);

				commands.Finish();

				var target = new double[w,h];
				for (var i = 0; i < w; i++)
					for (var j = 0; j < h; j++)
					{
						target[i, j] = totalValues[i + j*w];
					}

				return target;
			}
		}

	}
}

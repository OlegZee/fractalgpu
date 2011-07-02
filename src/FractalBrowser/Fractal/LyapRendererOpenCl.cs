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

			// a -> 0
			var mask = settings.Pattern.ToCharArray().Select(c => c == 'a' ? 0 : 1).ToArray();

			var platform = ComputePlatform.Platforms[0];
            var properties = new ComputeContextPropertyList(platform);
            var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero);

			Debug.WriteLine(string.Format("Using platform {0}, devices: {1}", platform.Name, 
				string.Join(", ", platform.Devices.Select(device => device.Name).ToArray()))
			);

			// create data buffers
			var aData = new ComputeBuffer<float>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, aValues);
			var bData = new ComputeBuffer<float>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, bValues);
			var maskData = new ComputeBuffer<int>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer, mask);

			var totalValues = new float[bValues.Length * aValues.Length];
			var totalData = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, totalValues.Length);

			var program = new ComputeProgram(context, Resources.LyapRendererOpenCl);
			program.Build(null, null, null, IntPtr.Zero);

			var kernel = program.CreateKernel("Lyapunov");
			kernel.SetMemoryArgument(0, aData);
			kernel.SetMemoryArgument(1, bData);
			kernel.SetMemoryArgument(2, maskData);
			kernel.SetMemoryArgument(3, totalData);
			kernel.SetValueArgument(4, (float)settings.InitialValue);
			kernel.SetValueArgument(5, settings.Warmup);
			kernel.SetValueArgument(6, mask.Length);
			kernel.SetValueArgument(7, settings.Iterations);
			kernel.SetValueArgument(8, bValues.Length);
			kernel.SetValueArgument(9, 1f / (settings.Iterations - settings.Warmup));

			// TODO reduce number of args

			var eventList = new ComputeEventList();
			var commands = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);

			commands.Execute(kernel, null, new long[] { aValues.Length, bValues.Length }, null, eventList);
			commands.ReadFromBuffer(totalData, ref totalValues, false, eventList);

			commands.Finish();

			var target = new double[w, h];
			for (var i = 0; i < w; i++)
				for (var j = 0; j < h; j++)
				{
					target[i, j] = totalValues[i + j * w];
				}

			return target;
		}

	}
}

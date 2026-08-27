using System.Diagnostics;
using Cloo;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// Performance-tuned OpenCL fractal renderer (LyapunovPerf.c kernel).
    /// Optimizations vs <see cref="LyapRendererOpenCl"/>: native_log2 with ln2 folded into the
    /// output scale, pattern held as a private bitmask (no per-iteration global load/modulo),
    /// one log2 per 4 iterations (product of |dF|), compile-time pattern specialization via
    /// -D PAT_BITS/PAT_LEN/PHASE0, build options "-cl-fast-relaxed-math -cl-mad-enable", and
    /// cached context/queue/program/kernel across renders.
    /// Note: -cl-fast-relaxed-math makes the output statistically equivalent but not
    /// pixel-reproducible against the reference GPU path; runs on the same device are
    /// deterministic. <see cref="LyapRendererOpenCl"/> remains the pixel-exact reference.
    /// </summary>
    public class LyapRendererOpenClPerf : LyapRendererBase
    {
        private sealed class DeviceCache
        {
            public required ComputeContext Context { get; init; }
            public required ComputeCommandQueue Queue { get; init; }
            public ComputeProgram? Program;
            public ComputeKernel? Kernel;
            public string? BuildOptions;
            public readonly object Lock = new();
        }

        // Cache is keyed by OpenCL device index; program/kernel additionally keyed by
        // build options (pattern/defines). Static so repeated DeviceRegistry factory calls
        // reuse the compiled program; guarded by locks for background-thread rendering.
        private static readonly Dictionary<int, DeviceCache> Caches = new();
        private static readonly object CachesLock = new();

        private readonly int _deviceIndex;
        private readonly ComputePlatform _platform;
        private readonly ComputeDevice _device;

        public LyapRendererOpenClPerf(int deviceIndex = 0)
        {
            _deviceIndex = deviceIndex;
            (_platform, _device) = OpenClDevices.GetByIndex(deviceIndex);
        }

        public override string ToString() => $"{nameof(LyapRendererOpenClPerf)}[{_device.Name}]";

        public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
        {
            var bscale = (settings.B.End - settings.B.Start) / w;
            var ascale = (settings.A.End - settings.A.Start) / h;

            var aValuesRaw = Enumerable.Range(0, h).Select(j => (float)(settings.A.Start + (j) * ascale)).ToArray();
            var bValuesRaw = Enumerable.Range(0, w).Select(i => (float)(settings.B.Start + (i) * bscale)).ToArray();

            var mask = settings.Pattern.Select(c => c == 'a' ? 0 : 1).ToArray();

            Debug.WriteLine("Using platform {0}, device: {1}", _platform.Name, _device.Name);

            var hsplit = 1;
            // empirical rule to split the data for better performance
            while (w * h / hsplit > 2 << 20)
            {
                hsplit *= 2;
            }

            var chunkLen = aValuesRaw.Length / hsplit;

            var resultData = new float[hsplit][];
            const ComputeMemoryFlags roBufferFlags = ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.UseHostPointer;

            var buildOptions = "-cl-fast-relaxed-math -cl-mad-enable";
            if (mask.Length <= 32)
            {
                var patBits = 0u;
                for (var k = 0; k < mask.Length; k++)
                    patBits |= (uint)(mask[k] != 0 ? 1 : 0) << k;
                buildOptions += $" -D PAT_BITS={patBits}u -D PAT_LEN={mask.Length} -D PHASE0={settings.Warmup % mask.Length}";
            }

            DeviceCache cache;
            lock (CachesLock)
            {
                if (!Caches.TryGetValue(_deviceIndex, out cache!))
                {
                    var properties = new ComputeContextPropertyList(_platform);
                    var context = new ComputeContext(new List<ComputeDevice> { _device }, properties, null, IntPtr.Zero);
                    cache = new DeviceCache
                    {
                        Context = context,
                        Queue = new ComputeCommandQueue(context, _device, ComputeCommandQueueFlags.None),
                    };
                    Caches.Add(_deviceIndex, cache);
                }
            }

            // the command queue (and program rebuild) must not be used concurrently
            lock (cache.Lock)
            {
                if (cache.Program == null || cache.BuildOptions != buildOptions)
                {
                    cache.Kernel?.Dispose();
                    cache.Program?.Dispose();
                    cache.Program = new ComputeProgram(cache.Context, Resources.LyapunovPerf);
                    try
                    {
                        cache.Program.Build(new List<ComputeDevice> { _device }, buildOptions, null, IntPtr.Zero);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(cache.Program.GetBuildLog(_device));
                        throw;
                    }
                    cache.Kernel = cache.Program.CreateKernel("Lyapunov");
                    cache.BuildOptions = buildOptions;
                }

                var context = cache.Context;
                var commands = cache.Queue;
                var kernelFunction = cache.Kernel!;

                var disposables = new List<IDisposable>();
                var bData = new ComputeBuffer<float>(context, roBufferFlags, bValuesRaw);
                var maskData = new ComputeBuffer<int>(context, roBufferFlags, mask);
                disposables.AddRange(new IDisposable[] { bData, maskData });

                var eventList = new ComputeEventList();

                var aPartitions = aValuesRaw.Chunk(chunkLen).ToList();
                for (var chunkIndex = 0; chunkIndex < hsplit; chunkIndex++)
                {
                    var aData = new ComputeBuffer<float>(context, roBufferFlags, aPartitions[chunkIndex]);
                    var resultBuffer = new ComputeBuffer<float>(context, ComputeMemoryFlags.WriteOnly, chunkLen * bValuesRaw.Length);
                    disposables.AddRange(new[] { aData, resultBuffer });

                    resultData[chunkIndex] = new float[chunkLen * bValuesRaw.Length];

                    kernelFunction.SetMemoryArgument(0, bData);
                    kernelFunction.SetMemoryArgument(1, aData);
                    kernelFunction.SetMemoryArgument(2, resultBuffer);
                    kernelFunction.SetMemoryArgument(3, maskData);
                    kernelFunction.SetValueArgument(4, (float)settings.InitialValue);
                    kernelFunction.SetValueArgument(5, settings.Warmup);
                    kernelFunction.SetValueArgument(6, settings.Iterations);
                    kernelFunction.SetValueArgument(7, mask.Length);
                    kernelFunction.SetValueArgument(8, 1f / (settings.Iterations - settings.Warmup));

                    commands.Execute(kernelFunction, null, new long[] { bValuesRaw.Length / 4, chunkLen }, null, eventList);
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

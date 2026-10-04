using System.Runtime.InteropServices;
using FractalGpu.Rendering.Fractal;
using Foundation;
using Metal;

namespace FractalGpu.Rendering.Metal;

/// <summary>
/// Lyapunov renderer on Metal: a straight port of <see cref="LyapRendererOpenCl"/> and its kernel.
/// Compiled pipelines are cached per device because compiling the shader source costs tens of milliseconds.
/// </summary>
public class LyapRendererMetal : LyapRendererBase
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LyapunovParams
    {
        public float InitialX;
        public int WarmupCount;
        public int IterationsCount;
        public int MaskLen;
        public float Divider;
        public uint RowStride;
    }

    private static readonly Dictionary<string, IMTLComputePipelineState> PipelineCache = new();

    private readonly IMTLDevice _device;
    private readonly IMTLCommandQueue _queue;

    public LyapRendererMetal(int deviceIndex = 0)
    {
        _device = MetalDevices.GetByIndex(deviceIndex);
        _queue = _device.CreateCommandQueue() ?? throw new InvalidOperationException("Metal: cannot create a command queue.");
    }

    public override string ToString() => $"{GetType().Name}[{_device.Name}]";

    /// <summary>Shader source for this variant.</summary>
    protected virtual string KernelSource => MetalResources.Lyapunov;

    /// <summary>
    /// Compile options for this variant. The reference path is built without fast-math (only the log is 'fast'),
    /// mirroring how the OpenCL reference kernel is built.
    /// </summary>
    protected virtual MTLCompileOptions CreateCompileOptions(Lyapunov.Settings settings, int[] mask) =>
        new() { FastMathEnabled = false };

    private IMTLComputePipelineState GetPipeline(Lyapunov.Settings settings, int[] mask)
    {
        var options = CreateCompileOptions(settings, mask);
        var macros = options.PreprocessorMacros is { } dict
            ? string.Join(";", dict.Keys.Select(k => $"{k}={dict[k]}").OrderBy(x => x))
            : "";
        var key = $"{GetType().Name}#{_device.RegistryId}#{options.FastMathEnabled}#{macros}";

        lock (PipelineCache)
        {
            if (PipelineCache.TryGetValue(key, out var cached)) return cached;

            var library = _device.CreateLibrary(KernelSource, options, out NSError? error)
                          ?? throw new InvalidOperationException($"Metal shader compilation failed: {error?.LocalizedDescription}");
            using var function = library.CreateFunction("Lyapunov")
                                 ?? throw new InvalidOperationException("Metal: kernel 'Lyapunov' not found in the compiled library.");
            var pipeline = _device.CreateComputePipelineState(function, out error)
                           ?? throw new InvalidOperationException($"Metal pipeline creation failed: {error?.LocalizedDescription}");

            PipelineCache[key] = pipeline;
            return pipeline;
        }
    }

    public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
    {
        if (w % 4 != 0)
            throw new ArgumentException("Metal renderer requires the width to be a multiple of 4.", nameof(w));

        var bscale = (settings.B.End - settings.B.Start) / w;
        var ascale = (settings.A.End - settings.A.Start) / h;

        var aValues = Enumerable.Range(0, h).Select(j => (float)(settings.A.Start + j * ascale)).ToArray();
        var bValues = Enumerable.Range(0, w).Select(i => (float)(settings.B.Start + i * bscale)).ToArray();
        var mask = settings.Pattern.Select(c => c == 'a' ? 0 : 1).ToArray();
        var pipeline = GetPipeline(settings, mask);

        // Same empirical split as the OpenCL path: keep each command buffer to ~2M pixels so that
        // long renders stay well under the GPU watchdog on mobile devices.
        var hsplit = 1;
        while (w * h / hsplit > 2 << 20) hsplit *= 2;
        var chunkLen = h / hsplit;

        var parameters = new LyapunovParams
        {
            InitialX = (float)settings.InitialValue,
            WarmupCount = settings.Warmup,
            IterationsCount = settings.Iterations,
            MaskLen = mask.Length,
            Divider = 1f / (settings.Iterations - settings.Warmup),
            RowStride = (uint)w,
        };

        const MTLResourceOptions shared = MTLResourceOptions.StorageModeShared;
        using var bBuffer = _device.CreateBuffer(bValues, shared) ?? throw Oom();
        using var aBuffer = _device.CreateBuffer(aValues, shared) ?? throw Oom();
        using var maskBuffer = _device.CreateBuffer(mask, shared) ?? throw Oom();
        using var resultBuffer = _device.CreateBuffer((nuint)(sizeof(float) * w * h), shared) ?? throw Oom();

        var width = (int)Math.Min(pipeline.ThreadExecutionWidth, (nuint)(w / 4));
        var height = (int)Math.Max(1, Math.Min(pipeline.MaxTotalThreadsPerThreadgroup / (nuint)width, (nuint)chunkLen));
        var threadsPerGroup = new MTLSize(width, height, 1);

        IMTLCommandBuffer? last = null;
        for (var chunkIndex = 0; chunkIndex < hsplit; chunkIndex++)
        {
            var rowOffset = chunkIndex * chunkLen;
            var commandBuffer = _queue.CommandBuffer() ?? throw new InvalidOperationException("Metal: cannot create a command buffer.");
            using (var encoder = commandBuffer.ComputeCommandEncoder ?? throw new InvalidOperationException("Metal: cannot create a compute encoder."))
            {
                encoder.SetComputePipelineState(pipeline);
                encoder.SetBuffer(bBuffer, 0, 0);
                encoder.SetBuffer(aBuffer, (nuint)(rowOffset * sizeof(float)), 1);
                encoder.SetBuffer(resultBuffer, (nuint)(rowOffset * w * sizeof(float)), 2);
                encoder.SetBuffer(maskBuffer, 0, 3);
                unsafe
                {
                    encoder.SetBytes((IntPtr)(&parameters), (nuint)sizeof(LyapunovParams), 4);
                }
                encoder.DispatchThreads(new MTLSize(w / 4, chunkLen, 1), threadsPerGroup);
                encoder.EndEncoding();
            }
            commandBuffer.Commit();
            last?.Dispose();
            last = commandBuffer;
        }

        last!.WaitUntilCompleted();
        if (last.Error is not null)
            throw new InvalidOperationException($"Metal command buffer failed: {last.Error.LocalizedDescription}");
        last.Dispose();

        var flat = new float[w * h];
        Marshal.Copy(resultBuffer.Contents, flat, 0, flat.Length);

        var target = new float[w, h];
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            for (var i = 0; i < w; i++)
                target[i, j] = flat[row + i];
        }
        return target;
    }

    private static InvalidOperationException Oom() => new("Metal: buffer allocation failed.");
}

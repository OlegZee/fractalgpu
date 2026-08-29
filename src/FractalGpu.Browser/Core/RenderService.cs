using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FractalGpu.Rendering.Common;
using FractalGpu.Rendering.Fractal;

namespace FractalGpu.Browser.Core;

/// <summary>Everything needed to produce one exponent map.</summary>
public sealed record RenderJob(
    FractalView View,
    string Pattern,
    double InitialValue,
    int Warmup,
    int Iterations,
    int DeviceIndex,
    int Width,
    int Height)
{
    public Lyapunov.Settings ToSettings(int width, int height) => new()
    {
        Size = new Sz(width, height),
        A = View.A,
        B = View.B,
        InitialValue = InitialValue,
        Warmup = Warmup,
        Iterations = Math.Max(Iterations, Warmup + 1),
        Pattern = string.IsNullOrEmpty(Pattern) ? "ab" : Pattern,
    };
}

/// <summary>A finished exponent map plus what it took to produce it.</summary>
public sealed record RenderOutput(
    float[,] Map,
    FractalView View,
    int Width,
    int Height,
    bool IsPreview,
    TimeSpan Elapsed,
    string DeviceName,
    long Generation);

/// <summary>
/// Serialises rendering onto one background worker.
/// <list type="bullet">
/// <item>Requests <b>coalesce</b>: while a render runs, only the newest request survives, so dragging
/// the view never queues a backlog of dead frames.</item>
/// <item>Rendering is <b>progressive</b>: a coarse pass appears first and is replaced by the full-resolution
/// one, unless the device proved fast enough last time that the coarse pass would only add latency.</item>
/// <item>The library exposes no cancellation, so an obsolete pass is allowed to finish and its result is
/// dropped by generation check.</item>
/// </list>
/// </summary>
public sealed class RenderService : IAsyncDisposable
{
    private static readonly int[] PreviewDivisors = [8, 3];
    private const double FastDeviceMilliseconds = 90;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _deviceGate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private RenderJob? _pending;
    private long _generation;
    private double _lastFullMilliseconds = double.MaxValue;

    private LyapRendererBase? _renderer;
    private int _rendererIndex = -1;
    private string _rendererName = "";

    public RenderService() => _worker = Task.Factory.StartNew(
        WorkerLoop, TaskCreationOptions.LongRunning).Unwrap();

    /// <summary>Raised on the worker thread whenever a pass completes; marshal to the UI yourself.</summary>
    public event Action<RenderOutput>? Completed;

    public event Action<string>? Failed;

    /// <summary>Raised on the worker thread when the queue goes busy/idle.</summary>
    public event Action<bool>? BusyChanged;

    public long CurrentGeneration
    {
        get { lock (_gate) return _generation; }
    }

    public void Request(RenderJob job)
    {
        lock (_gate)
        {
            _pending = job;
            _generation++;
        }
        _signal.Release();
    }

    private async Task WorkerLoop()
    {
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            RenderJob? job;
            long generation;
            lock (_gate)
            {
                job = _pending;
                _pending = null;
                generation = _generation;
            }

            if (job is null || job.Width <= 0 || job.Height <= 0) continue;

            BusyChanged?.Invoke(true);
            try
            {
                RunLadder(job, generation, token);
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex.Message);
            }
            finally
            {
                if (IsCurrent(generation)) BusyChanged?.Invoke(false);
            }
        }
    }

    private void RunLadder(RenderJob job, long generation, CancellationToken token)
    {
        if (_lastFullMilliseconds > FastDeviceMilliseconds)
        {
            foreach (var divisor in PreviewDivisors)
            {
                var w = Math.Max(1, job.Width / divisor);
                var h = Math.Max(1, job.Height / divisor);
                if (w >= job.Width || h >= job.Height) continue;

                if (!IsCurrent(generation) || token.IsCancellationRequested) return;
                var preview = RenderPass(job, w, h, isPreview: true, generation);
                if (preview is null) return;
                if (!IsCurrent(generation)) return;

                Completed?.Invoke(preview);

                // A coarse pass that already cost real time means the full pass is the only thing worth waiting for.
                if (preview.Elapsed.TotalMilliseconds > FastDeviceMilliseconds) break;
            }
        }

        if (!IsCurrent(generation) || token.IsCancellationRequested) return;

        var full = RenderPass(job, job.Width, job.Height, isPreview: false, generation);
        if (full is null || !IsCurrent(generation)) return;

        _lastFullMilliseconds = full.Elapsed.TotalMilliseconds;
        Completed?.Invoke(full);
    }

    private bool IsCurrent(long generation)
    {
        lock (_gate) return _generation == generation && _pending is null;
    }

    private RenderOutput? RenderPass(RenderJob job, int width, int height, bool isPreview, long generation)
    {
        _deviceGate.Wait();
        try
        {
            var renderer = GetRenderer(job.DeviceIndex);
            var settings = job.ToSettings(width, height);

            var sw = Stopwatch.StartNew();
            var map = renderer.RenderImpl(width, height, settings);
            sw.Stop();

            return new RenderOutput(map, job.View, width, height, isPreview, sw.Elapsed, _rendererName, generation);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex.Message);
            return null;
        }
        finally
        {
            _deviceGate.Release();
        }
    }

    /// <summary>Renders a one-off map at an arbitrary size (image export) without disturbing the live view.</summary>
    public async Task<RenderOutput> RenderOnceAsync(RenderJob job, int width, int height, CancellationToken token = default)
    {
        await _deviceGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var renderer = GetRenderer(job.DeviceIndex);
            var settings = job.ToSettings(width, height);

            var sw = Stopwatch.StartNew();
            var map = await Task.Run(() => renderer.RenderImpl(width, height, settings), token).ConfigureAwait(false);
            sw.Stop();

            return new RenderOutput(map, job.View, width, height, false, sw.Elapsed, _rendererName, -1);
        }
        finally
        {
            _deviceGate.Release();
        }
    }

    /// <summary>
    /// Renderer instances are cached per device: rebuilding an <c>LyapRendererOpenCl</c> re-enumerates
    /// platforms and recompiles the kernel, which would dominate an interactive frame.
    /// </summary>
    private LyapRendererBase GetRenderer(int deviceIndex)
    {
        if (_renderer is not null && _rendererIndex == deviceIndex) return _renderer;

        var descriptor = Devices.GetByIndex(deviceIndex);
        _renderer = descriptor.CreateRenderer();
        _rendererIndex = deviceIndex;
        _rendererName = descriptor.Name;
        return _renderer;
    }

    /// <summary>Forces the timing heuristic to re-learn, e.g. after the user switches device.</summary>
    public void ResetTimingHeuristic() => _lastFullMilliseconds = double.MaxValue;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _signal.Release();
        try { await _worker.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        _cts.Dispose();
        _signal.Dispose();
        _deviceGate.Dispose();
    }
}

/// <summary>
/// Caches <see cref="DeviceRegistry.Enumerate"/>, which re-enumerates OpenCL platforms on every call.
/// </summary>
public static class Devices
{
    private static IReadOnlyList<DeviceDescriptor>? _cached;
    private static string? _openClError;

    public static IReadOnlyList<DeviceDescriptor> All
    {
        get
        {
            if (_cached is not null) return _cached;
            _cached = DeviceRegistry.Enumerate(out _openClError);
            return _cached;
        }
    }

    public static string? OpenClError
    {
        get { _ = All; return _openClError; }
    }

    public static DeviceDescriptor GetByIndex(int index) =>
        All.FirstOrDefault(d => d.Index == index)
        ?? throw new ArgumentException($"Device index {index} is out of range (0..{All.Count - 1}).");

    public static int DefaultIndex()
    {
        var gpu = All.FirstOrDefault(d => d.Kind == DeviceKind.OpenCl);
        if (gpu is not null) return gpu.Index;

        var perf = All.FirstOrDefault(d => d.Kind == DeviceKind.MultiCorePerf);
        return perf?.Index ?? 1;
    }
}

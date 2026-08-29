# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FractalGPU renders Lyapunov fractals on multiple backends (single-core CPU, multi-core CPU, GPU via OpenCL/Cloo). Requires .NET SDK 10.0 (pinned in `global.json`). The solution `FractalGpu.slnx` contains four projects:

- **FractalGpu.Rendering** — shared library; the source of truth for all fractal logic, device selection, and OpenCL interop
- **RenderCli** — multi-mode CLI (`benchmark` / `render` / `list-devices` subcommands); a single `Program.cs` on top of the library
- **FractalGpu.RenderServer** — ASP.NET Core queue-driven render API
- **FractalGpu.Browser** — Avalonia desktop app for interactive exploration (pan/zoom/history/export)

`src/FractalBrowser` (legacy WinForms, .NET Framework 3.5) is outside the solution, carries its own old copies of the rendering code, and must not be modified unless the task explicitly requires it.

## Development Commands

```bash
# Build everything
dotnet build FractalGpu.slnx -c Release

# CLI: no arguments = help; work happens in subcommands
dotnet run -c Release --project src/RenderCli -- list-devices
dotnet run -c Release --project src/RenderCli -- benchmark --device 0   # index from list-devices; repeatable (-d 0 2) to compare devices
dotnet run -c Release --project src/RenderCli -- benchmark              # no --device: benchmarks all devices, prints comparison summary
dotnet run -c Release --project src/RenderCli -- render -d 0 -o out.bmp # render to BMP on a device; --size, --iterations, --pattern optional

# Interactive browser (Avalonia desktop app)
dotnet run -c Release --project src/FractalGpu.Browser

# Render server (http://localhost:5229, see Properties/launchSettings.json)
dotnet run --project src/FractalGpu.RenderServer
# Smoke request (all query params are required by validation):
# GET /api/fractal/render?width=64&height=64&startA=2&endA=4&startB=2&endB=4&initial=0.5&pattern=ab&warmup=10&iterations=1000&contrast=1.7
```

There is no automated test suite. Validate changes by running the benchmark on the relevant device(s) and, for server changes, issuing a render request. The escalating benchmark self-terminates (stops once a step takes ≥ 2.5 s). For browser changes, run it and pan/zoom on both a CPU and a GPU device; it can also be driven without a window server via `Avalonia.Headless` + `Avalonia.Skia` (`UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`, then `window.CaptureRenderedFrame()`), which is how it was validated here. Run the test body **inside** the dispatcher loop (`Dispatcher.UIThread.MainLoop`) — hand-pumping `RunJobs()` delivers `DispatcherTimer` callbacks, and therefore the render debounce, tens of seconds late.

## Architecture

### Rendering pipeline (FractalGpu.Rendering)

- `FractalRenderer<TSettings> where TSettings : RenderSettings` is the generic base: it takes settings, calls the subclass's `RenderImpl(w, h, settings)` to get a `float[,]` exponent map, and maps it to a `Media/RawBitmap` via a color function.
- `LyapRendererBase : FractalRenderer<Lyapunov.Settings>` closes the generic over Lyapunov settings and supplies the coloring. Its subclasses are the backends: `LyapRendererCpu`, `LyapRendererCpuPerf : LyapRendererCpu` (the name says "perf", not "SIMD", because the deferred log dominates the speedup; vectorizes the inner loop over adjacent A-axis pixels with variable-width `Vector<double>`; uses a deferred log — running product of derivatives with exact IEEE-754 exponent extraction into an integer accumulator every 10 iterations, one `Math.Log2` per pixel at the end — plus a 4/2/1-block interleave cascade for ILP; lanes flagged bad at renormalization (zero/denormal/Inf/NaN) and row tails fall back to the inherited scalar code; output is byte-identical to the scalar renderer — keep that property when touching it), `LyapRendererMulticore<T>` (splits the A-range into tiles — 256 by convention — dispatched over the ThreadPool; `T` is any `LyapRendererBase` with a parameterless ctor, so it composes with the optimized renderer as `LyapRendererMulticore<LyapRendererCpuPerf>`; each tile's A sub-range is derived from its **actual row bounds**, so heights that are not a multiple of the tile count still render at the right scale — the earlier equal-range split gave the last tile the leftover rows but only 1/tileCount of the range, which wrecked the whole image for such heights, and tile counts above the image height are clamped. Tiling can never be bit-exact against the single-core path, because a tile re-derives its step from its own sub-range and lands one ulp away; what must hold is that no strip renders at a visibly different scale), `LyapRendererOpenCl(int deviceIndex = 0)` (builds a `ComputeContext` over the single selected device; the kernel source is the embedded resource `Resources/Lyapunov.c`, loaded via `Resources.cs` using the assembly's `RootNamespace`), and `LyapRendererOpenClPerf(int deviceIndex = 0)` (performance-tuned GPU path on `Resources/LyapunovPerf.c`: `-cl-fast-relaxed-math -cl-mad-enable`, pattern bitmask in registers, one `native_log2` per 4 iterations with ln2 folded into the scale, compile-time pattern defines `-D PAT_BITS/PAT_LEN/PHASE0`, static context/program cache keyed by device index + build options; ~2.3x the regular GPU but not pixel-reproducible against it due to fast-math — it must NOT replace `LyapRendererOpenCl`, which stays the pixel-reproducible reference).
- `RenderSettings` and `Lyapunov.Settings` are **records with `init` properties**. Construct with object initializers and derive variants with `with` expressions (`settings = settings with { Iterations = n, ... }`); do not reintroduce fluent `Set*` builder methods.

### Device selection

`Fractal/DeviceRegistry.cs` exposes the unified, index-addressable device list used by both RenderCli and RenderServer: index 0 = single-core CPU, 1 = multi-core CPU, 2 = single-core perf (deferred log + SIMD), 3 = multi-core perf, 4+ = OpenCL devices in enumeration order, followed by one `(perf)` entry per OpenCL device (`LyapRendererOpenClPerf`, fast-math GPU path). `GetByIndex` throws with a neutral message; `DefaultIndex()` prefers the first *regular* GPU (never a `(perf)` entry), falling back to multi-core perf. CLI-specific presentation (printing the device table, the "Run 'list-devices'" hint on errors) deliberately lives in `RenderCli/Program.cs`, not in the library.

`Fractal/OpenClDevices.cs` does raw platform/device enumeration. Its public surface is `EnumerateInfo()` returning the Cloo-free `OpenClDeviceInfo` DTO; Cloo types stay `internal` to the library. **Consumers (including RenderCli) must never reference Cloo directly** — RenderCli intentionally has no Cloo package reference.

### macOS OpenCL loading

Handled automatically by `Fractal/OpenClLibraryResolver.cs` (`NativeLibrary.SetDllImportResolver` on the Cloo assembly, trying the system OpenCL framework paths), invoked from `OpenClDevices`. No `DYLD_LIBRARY_PATH` or per-project configuration is needed — do not reintroduce environment-variable workarounds; setting `DYLD_LIBRARY_PATH` in-process does not work.

### Browser (FractalGpu.Browser)

Avalonia 12.1.1 on `net10.0`; no MVVM framework (a 30-line `ObservableObject`/`RelayCommand` pair in `ViewModels/`). `Core/` is deliberately Avalonia-free and holds everything testable:

- `FractalView` — the visible (A,B) rectangle. **B is horizontal, A is vertical and grows upward**, matching `LyapRendererCpu.RenderImpl` (`b = B.Start + i*bscale`, `a = A.Start + j*ascale`) plus the bottom-up flip in `FractalRenderer.CreateBitmap`; screen row `y` is `map[x, h-1-y]`. `FitAspect` equalises the per-pixel scale on both axes and then slides the widened axis back inside the 0..4 domain so a preset does not park a band of `r > 4` divergence at the window edge.
- `Palette` — colour mapping from the cached `float[,]` exponent map. It reproduces `LyapRendererBase.ColorFromExp`'s **integer truncation** (`(int)(exp(e)*255)`, `(byte)(i*0.85)`), not the mathematically cleaner rounding; that is what makes `Palette.Classic` byte-identical to `RenderCli render` output — keep that property when touching it. The contrast gamma is tabulated over the 256 possible indices rather than evaluated per pixel.
- `RenderService` — one background worker. Requests **coalesce** (only the newest survives), each render is **progressive** (coarse passes then full, skipped when the last full render was under 90 ms), and renderer instances are cached per device because rebuilding `LyapRendererOpenCl` re-enumerates platforms and recompiles the kernel. The library has no cancellation, so a stale pass runs to completion and its result is dropped by generation check.

`Views/FractalCanvas` is a custom `Control` that keeps the last exponent map together with the `FractalView` it was rendered for, and draws it into the *current* view — so pan and zoom resample the existing bitmap immediately while a fresh render is still in flight. The view model owns the debounce, history and settings; the canvas raises `ViewChanged`/`CursorMoved`. They are wired directly in `MainWindow`'s code-behind rather than through bindings, because an exponent map and a pixel buffer are not view-model shaped data.

Render sizes are rounded up to a multiple of 16 and the view widened by the same proportion (the canvas clips the surplus): the OpenCL kernel consumes four B-values per work item and splits rows into power-of-two chunks, so ragged sizes silently drop trailing columns or rows. The GPU paths sample the axes through `float` tables, so the status bar warns below ~2.4e-7 per pixel, where the GPU stair-steps and the double-precision CPU paths do not.

### RenderServer flow

`Controllers/FractalController.cs` validates a `FractalRequest` DTO (GET and POST funnel into the same handler), maps it to `Lyapunov.Settings`, and awaits `IRenderQueue.QueueRenderAsync`. `Services/RenderBackgroundService.cs` drains the queue and creates its renderer via `DeviceRegistry.GetByIndex(DeviceRegistry.DefaultIndex())`.

## Conventions and Gotchas

- CLI parsing uses **System.CommandLine 2.0 GA** (`Option<T>` object initializers, `command.SetAction(parseResult => ...)`, `rootCommand.Parse(args).Invoke()`). The widely-documented beta APIs (`AddOption`, `SetHandler`, `InvokeAsync(args)`) do not exist in GA and will not compile.
- Never duplicate rendering logic across projects — extend `FractalGpu.Rendering` and reference it. The only sanctioned duplication is the frozen legacy FractalBrowser.
- `libs/` holds checked-in legacy binaries (Microsoft Accelerator for FractalBrowser); don't touch without coordination.
- Historical docs (`PRPs/`, `.serena/`, `.qwen/`) describe past project states and are intentionally not kept up to date; `readme.md` and `CLAUDE.md` are the living documentation.

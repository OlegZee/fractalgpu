# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FractalGPU renders Lyapunov fractals on multiple backends (single-core CPU, multi-core CPU, GPU via OpenCL/Cloo). Requires .NET SDK 10.0 (pinned in `global.json`). The solution `FractalGpu.slnx` contains three projects:

- **FractalGpu.Rendering** — shared library; the source of truth for all fractal logic, device selection, and OpenCL interop
- **RenderCli** — multi-mode CLI (`benchmark` / `list-devices` subcommands); a single `Program.cs` on top of the library
- **FractalGpu.RenderServer** — ASP.NET Core queue-driven render API

`src/FractalBrowser` (legacy WinForms, .NET Framework 3.5) is outside the solution, carries its own old copies of the rendering code, and must not be modified unless the task explicitly requires it.

## Development Commands

```bash
# Build everything
dotnet build FractalGpu.slnx -c Release

# CLI: no arguments = help; work happens in subcommands
dotnet run -c Release --project src/RenderCli -- list-devices
dotnet run -c Release --project src/RenderCli -- benchmark --device 0   # index from list-devices
dotnet run -c Release --project src/RenderCli -- benchmark              # default: first GPU, else multi-core CPU

# Render server (http://localhost:5229, see Properties/launchSettings.json)
dotnet run --project src/FractalGpu.RenderServer
# Smoke request (all query params are required by validation):
# GET /api/fractal/render?width=64&height=64&startA=2&endA=4&startB=2&endB=4&initial=0.5&pattern=ab&warmup=10&iterations=1000&contrast=1.7
```

There is no automated test suite. Validate changes by running the benchmark on the relevant device(s) and, for server changes, issuing a render request. The escalating benchmark self-terminates (stops once a step takes ≥ 2.5 s).

## Architecture

### Rendering pipeline (FractalGpu.Rendering)

- `FractalRenderer<TSettings> where TSettings : RenderSettings` is the generic base: it takes settings, calls the subclass's `RenderImpl(w, h, settings)` to get a `float[,]` exponent map, and maps it to a `Media/RawBitmap` via a color function.
- `LyapRendererBase : FractalRenderer<Lyapunov.Settings>` closes the generic over Lyapunov settings and supplies the coloring. Its three subclasses are the backends: `LyapRendererCpu`, `LyapRendererMulticore<T>` (splits the A-range into tiles — 256 by convention — dispatched over the ThreadPool), and `LyapRendererOpenCl(int deviceIndex = 0)` (builds a `ComputeContext` over the single selected device; the kernel source is the embedded resource `Resources/Lyapunov.c`, loaded via `Resources.cs` using the assembly's `RootNamespace`).
- `RenderSettings` and `Lyapunov.Settings` are **records with `init` properties**. Construct with object initializers and derive variants with `with` expressions (`settings = settings with { Iterations = n, ... }`); do not reintroduce fluent `Set*` builder methods.

### Device selection

`Fractal/DeviceRegistry.cs` exposes the unified, index-addressable device list used by both RenderCli and RenderServer: index 0 = single-core CPU, 1 = multi-core CPU, 2+ = OpenCL devices in enumeration order. `GetByIndex` throws with a neutral message; `DefaultIndex()` prefers the first GPU. CLI-specific presentation (printing the device table, the "Run 'list-devices'" hint on errors) deliberately lives in `RenderCli/Program.cs`, not in the library.

`Fractal/OpenClDevices.cs` does raw platform/device enumeration. Its public surface is `EnumerateInfo()` returning the Cloo-free `OpenClDeviceInfo` DTO; Cloo types stay `internal` to the library. **Consumers (including RenderCli) must never reference Cloo directly** — RenderCli intentionally has no Cloo package reference.

### macOS OpenCL loading

Handled automatically by `Fractal/OpenClLibraryResolver.cs` (`NativeLibrary.SetDllImportResolver` on the Cloo assembly, trying the system OpenCL framework paths), invoked from `OpenClDevices`. No `DYLD_LIBRARY_PATH` or per-project configuration is needed — do not reintroduce environment-variable workarounds; setting `DYLD_LIBRARY_PATH` in-process does not work.

### RenderServer flow

`Controllers/FractalController.cs` validates a `FractalRequest` DTO (GET and POST funnel into the same handler), maps it to `Lyapunov.Settings`, and awaits `IRenderQueue.QueueRenderAsync`. `Services/RenderBackgroundService.cs` drains the queue and creates its renderer via `DeviceRegistry.GetByIndex(DeviceRegistry.DefaultIndex())`.

## Conventions and Gotchas

- CLI parsing uses **System.CommandLine 2.0 GA** (`Option<T>` object initializers, `command.SetAction(parseResult => ...)`, `rootCommand.Parse(args).Invoke()`). The widely-documented beta APIs (`AddOption`, `SetHandler`, `InvokeAsync(args)`) do not exist in GA and will not compile.
- Never duplicate rendering logic across projects — extend `FractalGpu.Rendering` and reference it. The only sanctioned duplication is the frozen legacy FractalBrowser.
- `libs/` holds checked-in legacy binaries (Microsoft Accelerator for FractalBrowser); don't touch without coordination.
- Historical docs (`PRPs/`, `.serena/`, `.qwen/`) describe past project states and are intentionally not kept up to date; `readme.md` and `CLAUDE.md` are the living documentation.

# FractalGPU

Copyright (c) 2025 FractalGPU Project. All rights reserved.

## Running Benchmark

Prerequisites:
- Dotnet SDK 10.0+

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

## RenderCli (multi-mode CLI)

`RenderCli` is a multi-mode command-line tool. It lets you list available render devices (CPU modes and OpenCL GPU devices), run the escalating render benchmark against a specific one, and render a fractal image to a BMP file.

List available devices:

```bash
dotnet run -c Release --project src/RenderCli -- list-devices
```

Run the benchmark on a specific device (single-core CPU):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 0
```

Run on all CPU cores:

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 1
```

Run the optimized CPU variants (deferred log + SIMD; single-core and multi-core):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 2
dotnet run -c Release --project src/RenderCli -- benchmark --device 3
```

Run on the first GPU:

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 4
```

Device indices: `0` single-core CPU, `1` multi-core CPU, `2` single-core optimized, `3` multi-core optimized, `4+` OpenCL devices. Use `list-devices` for the authoritative list on your machine.

### Rendering to a file

Render a Lyapunov fractal to a BMP file (defaults: preferred device, `fractal.bmp`, 512x512, 10000 iterations, pattern `ab`):

```bash
dotnet run -c Release --project src/RenderCli -- render
dotnet run -c Release --project src/RenderCli -- render -d 0 -o scalar.bmp --size 256 --iterations 2000
```

Options: `--device`/`-d` device index, `--output`/`-o` output path, `--size` square image size, `--iterations` per-pixel iterations (warmup is iterations/10), `--pattern` Lyapunov sequence. Rendering the same image on device `0` (scalar CPU) and device `2` (optimized) and comparing the files (`cmp a.bmp b.bmp`) is the quick correctness check for the optimized renderer — the outputs are byte-identical.

### Optimized CPU rendering

`LyapRendererCpuOptimized` combines two independent optimizations, and the deferred log — not SIMD — is where most of the speedup comes from. It vectorizes the Lyapunov inner loop with variable-width `System.Numerics.Vector<double>`: the same code path runs 128-bit NEON on ARM64 (Apple Silicon), 256-bit AVX2 on x64, and falls back to the scalar renderer where hardware acceleration is unavailable. On AVX-512 machines set `DOTNET_MaxVectorTBitWidth=512` to unlock 512-bit vectors.

Instead of a transcendental log per iteration it uses a deferred log (Benettin-style renormalization): the `|r·(1−2x)|` derivatives are accumulated into a running product whose IEEE-754 exponent is periodically moved into an integer accumulator with exact bit operations, leaving a single `Math.Log2` per pixel. Lanes that hit special values (zero derivative, Inf/NaN) are recomputed with the scalar code, so output stays byte-identical to the scalar renderer (~9× faster single-core on Apple M-series, more on wider vectors).

Run with no `--device` (benchmarks ALL available devices sequentially and prints a comparison summary):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

Compare several devices in one run (both forms are equivalent):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark -d 0 -d 2
dotnet run -c Release --project src/RenderCli -- benchmark -d 0 2
```

## Running under macOS

OpenCL library loading is handled automatically via `NativeLibrary.SetDllImportResolver` in the rendering code—no extra environment configuration is required.

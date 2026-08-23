# FractalGPU

Copyright (c) 2025 FractalGPU Project. All rights reserved.

## Running Benchmark

Prerequisites:
- Dotnet SDK 10.0+

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

## RenderCli (multi-mode CLI)

`RenderCli` is a multi-mode command-line tool. It lets you list available render devices (CPU modes and OpenCL GPU devices) and run the escalating render benchmark against a specific one.

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

Run on the first GPU:

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 2
```

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

# FractalGPU

Copyright (c) 2025 FractalGPU Project. All rights reserved.

## Running Benchmark

Prerequisites:
- Dotnet SDK 10.0+

```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

## RenderCli (multi-mode CLI)

`RenderCli` is a separate, multi-mode command-line tool — distinct from `FractalGpu.Benchmark` above. It lets you list available render devices (CPU modes and OpenCL GPU devices) and run the escalating render benchmark against a specific one.

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

Run with no `--device` (defaults to the first GPU, or multi-core CPU if none is available):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

## Running under macOS

OpenCL library loading is handled automatically via `NativeLibrary.SetDllImportResolver` in the rendering code—no extra environment configuration is required.

## Running macos WinViewer application

```bash
  cd ./src/FractalGpu.WinViewer
  dotnet run --framework net10.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/
- Double-click FractalGpu.WinViewer.app

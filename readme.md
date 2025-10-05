## Running Benchmark

Prerequisites:
- Dotnet SDK 9.0+

```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

## Running under macOS

OpenCL library loading is handled automatically via `NativeLibrary.SetDllImportResolver` in the rendering code—no extra environment configuration is required.

## Running macos WinViewer application

```bash
  cd ./src/FractalGpu.WinViewer
  dotnet run --framework net8.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: bin/Debug/net8.0-maccatalyst/maccatalyst-arm64/
- Double-click FractalGpu.WinViewer.app

## Running Benchmark

Prerequisites:
- Dotnet SDK 9.0+

```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

## Running under Macos

There's unclear issue with OpenCL path resolution, which I suppose is a netcore issue.

In order to fix the issue run the following command before executing application:

```bash
export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework
```

## Running macos WinViewer application

```bash
  cd ./src/FractalGpu.WinViewer
  dotnet run --framework net8.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: bin/Debug/net8.0-maccatalyst/maccatalyst-arm64/
- Double-click FractalGpu.WinViewer.app
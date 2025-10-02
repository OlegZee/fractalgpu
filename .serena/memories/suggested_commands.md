# Suggested Commands for FractalGpu Development

## Prerequisites
- .NET SDK 9.0 or higher
- OpenCL drivers for GPU acceleration (if using OpenCL)
- On macOS, run this command before executing applications:
```bash
export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework
```

## Building and Running Applications

### Benchmark
```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

### Web API Server
```bash
cd src/FractalGpu.RenderServer
dotnet run
```

### GUI Application (macOS)
```bash
cd src/FractalGpu.WinViewer
dotnet run --framework net8.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: `bin/Debug/net8.0-maccatalyst/maccatalyst-arm64/`
- Double-click `FractalGpu.WinViewer.app`

## Development Commands

### Build the entire solution
```bash
dotnet build
```

### Run tests (if available)
```bash
dotnet test
```

### Clean build artifacts
```bash
dotnet clean
```

### Publish an application
```bash
dotnet publish src/FractalGpu.Benchmark/FractalGpu.Benchmark.csproj -c Release
```

## macOS Specific Notes
Due to OpenCL path resolution issues on macOS, always run the DYLD_LIBRARY_PATH export command before executing applications.
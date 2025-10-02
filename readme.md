## Running Benchmark

Prerequisites:
- Dotnet SDK 9.0+

```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

## Running under macOS

**No special configuration needed!** The OpenCL library loading is handled automatically via `NativeLibrary.SetDllImportResolver` in the rendering code.

<details>
<summary>Legacy DYLD_LIBRARY_PATH approach (deprecated)</summary>

The old workaround using `DYLD_LIBRARY_PATH` is no longer necessary:

```bash
# NOT NEEDED ANYMORE
export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework
```

This approach is considered bad practice on modern macOS and doesn't work reliably with System Integrity Protection (SIP).
</details>

## Running macos WinViewer application

```bash
  cd ./src/FractalGpu.WinViewer
  dotnet run --framework net8.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: bin/Debug/net8.0-maccatalyst/maccatalyst-arm64/
- Double-click FractalGpu.WinViewer.app
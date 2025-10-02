# System-Specific Information for Darwin/macOS

## Operating System Specifics
- Development platform: Darwin (macOS)
- File system: HFS+ or APFS
- Command-line tools follow BSD conventions rather than GNU (e.g., different flags for utilities like ls, find, etc.)

## macOS-Specific Issues
- OpenCL path resolution problem requiring DYLD_LIBRARY_PATH environment variable
- Command before running applications: `export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework`

## macOS-Specific Commands
- To check OpenCL availability: `clinfo` (if installed) or check OpenCL framework
- .NET MAUI application specific to macOS: Use `--framework net8.0-maccatalyst` flag when running the GUI application

## Useful Darwin/macOS Commands
- Check dotnet version: `dotnet --version`
- List installed SDKs: `dotnet --list-sdks`
- macOS-specific process management: `ps`, `top`, `activity monitor`
- File permissions: `ls -la`, `chmod`, `chown`

## Development Tools
- Visual Studio for Mac as an alternative IDE (though VS Code is commonly used)
- Xcode command line tools may be needed for certain operations
- Homebrew package manager for additional tools
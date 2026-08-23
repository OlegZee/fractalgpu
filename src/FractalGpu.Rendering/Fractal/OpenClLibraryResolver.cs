using System.Diagnostics;
using Cloo;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// Helper to resolve OpenCL library path on macOS without requiring DYLD_LIBRARY_PATH.
    /// </summary>
    internal static class OpenClLibraryResolver
    {
        private static bool _isInitialized;
        private static readonly object _lock = new();

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                // Set up DllImport resolver for Cloo assembly
                var clooAssembly = typeof(Cloo.ComputePlatform).Assembly;
                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                    clooAssembly,
                    DllImportResolver);

                _isInitialized = true;
                Trace.WriteLine("OpenCL library resolver initialized");
            }
        }

        private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, System.Runtime.InteropServices.DllImportSearchPath? searchPath)
        {
            // Only handle OpenCL library
            if (!libraryName.Equals("OpenCL", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;

            IntPtr handle = IntPtr.Zero;

            // Try macOS framework path first
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            {
                var macCandidates = new[]
                {
                    "/System/Library/Frameworks/OpenCL.framework/OpenCL",
                    "/System/Library/Frameworks/OpenCL.framework/Versions/Current/OpenCL",
                    "/System/Library/Frameworks/OpenCL.framework/Libraries/libOpenCL.dylib"
                };

                foreach (var frameworkPath in macCandidates)
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(frameworkPath, out handle))
                    {
                        Trace.WriteLine($"Loaded OpenCL from macOS framework: {frameworkPath}");
                        return handle;
                    }
                }
            }

            // Fall back to default loading for other platforms (Windows, Linux)
            // This will look for opencl.dll on Windows, libOpenCL.so on Linux
            if (System.Runtime.InteropServices.NativeLibrary.TryLoad(libraryName, assembly, searchPath, out handle))
            {
                Trace.WriteLine($"Loaded OpenCL using default resolver");
                return handle;
            }

            Trace.WriteLine($"Failed to load OpenCL library");
            return IntPtr.Zero;
        }
    }
}

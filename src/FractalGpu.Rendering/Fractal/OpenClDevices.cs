using Cloo;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// Public, Cloo-free description of an OpenCL device, for consumers that must not
    /// reference Cloo types directly.
    /// </summary>
    public sealed record OpenClDeviceInfo(
        int Index,
        string Name,
        string DeviceType,
        string PlatformName,
        string PlatformVersion,
        long MaxComputeUnits,
        long GlobalMemoryBytes,
        string DriverVersion,
        bool Available);

    /// <summary>
    /// Helpers for enumerating and selecting OpenCL platforms/devices.
    /// </summary>
    public static class OpenClDevices
    {
        /// <summary>
        /// Public, DTO-based device enumeration. Does not leak Cloo types.
        /// </summary>
        public static IReadOnlyList<OpenClDeviceInfo> EnumerateInfo()
        {
            OpenClLibraryResolver.Initialize();

            var devices = (from p in ComputePlatform.Platforms
                from d in p.Devices
                select (p, d)).ToList();

            var result = new List<OpenClDeviceInfo>(devices.Count);
            for (var i = 0; i < devices.Count; i++)
            {
                var (p, d) = devices[i];
                result.Add(new OpenClDeviceInfo(
                    i,
                    d.Name,
                    d.Type.ToString(),
                    p.Name,
                    p.Version,
                    d.MaxComputeUnits,
                    d.GlobalMemorySize,
                    d.DriverVersion,
                    d.Available));
            }

            return result;
        }

        internal static IReadOnlyList<(ComputePlatform Platform, ComputeDevice Device)> Enumerate()
        {
            OpenClLibraryResolver.Initialize();

            return (from p in ComputePlatform.Platforms
                from d in p.Devices
                select (p, d)).ToList();
        }

        internal static (ComputePlatform Platform, ComputeDevice Device) GetByIndex(int index)
        {
            var devices = Enumerate();
            if (devices.Count == 0)
                throw new InvalidOperationException("No OpenCL devices found (GPU/OpenCL drivers missing?)");

            if (index < 0 || index >= devices.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Device index {index} is out of range (0..{devices.Count - 1}).");

            return devices[index];
        }
    }
}

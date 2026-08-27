namespace FractalGpu.Rendering.Fractal
{
    public enum DeviceKind
    {
        Cpu,
        MultiCore,
        CpuOptimized,
        MultiCoreOptimized,
        OpenCl
    }

    public sealed record DeviceDescriptor(int Index, DeviceKind Kind, string Name, string Details, Func<LyapRendererBase> CreateRenderer);

    /// <summary>
    /// Unifies CPU render modes and OpenCL devices behind a single, index-addressable list.
    /// </summary>
    public static class DeviceRegistry
    {
        private const int MultiCoreTiles = 256;

        public static IReadOnlyList<DeviceDescriptor> Enumerate(out string? openClError)
        {
            var optimizedDetails = System.Numerics.Vector.IsHardwareAccelerated
                ? $"deferred log + SIMD: Vector<double> {System.Numerics.Vector<double>.Count} lanes ({System.Numerics.Vector<byte>.Count * 8}-bit)"
                : "not hardware accelerated, falls back to scalar";

            var devices = new List<DeviceDescriptor>
            {
                new(0, DeviceKind.Cpu, "CPU (single core)", "", () => new LyapRendererCpu()),
                new(1, DeviceKind.MultiCore, $"CPU (multi-core, {Environment.ProcessorCount} threads)", "",
                    () => new LyapRendererMulticore<LyapRendererCpu>(MultiCoreTiles)),
                new(2, DeviceKind.CpuOptimized, "CPU (single core, optimized)", optimizedDetails,
                    () => new LyapRendererCpuOptimized()),
                new(3, DeviceKind.MultiCoreOptimized, $"CPU (multi-core optimized, {Environment.ProcessorCount} threads)", optimizedDetails,
                    () => new LyapRendererMulticore<LyapRendererCpuOptimized>(MultiCoreTiles)),
            };

            openClError = null;
            try
            {
                var oclDevices = OpenClDevices.EnumerateInfo();
                foreach (var info in oclDevices)
                {
                    var details = $"({info.DeviceType})  platform: {info.PlatformName} {info.PlatformVersion}  CUs: {info.MaxComputeUnits}  mem: {info.GlobalMemoryBytes / (1024 * 1024)} MB  driver: {info.DriverVersion}";
                    if (!info.Available) details += "  [UNAVAILABLE]";

                    devices.Add(new DeviceDescriptor(devices.Count, DeviceKind.OpenCl, info.Name, details,
                        () => new LyapRendererOpenCl(info.Index)));
                }
            }
            catch (Exception ex)
            {
                openClError = ex.Message;
            }

            return devices;
        }

        public static DeviceDescriptor GetByIndex(int index)
        {
            var devices = Enumerate(out _);
            if (index < 0 || index >= devices.Count)
                throw new ArgumentException(
                    $"Device index {index} is out of range (0..{devices.Count - 1}).");

            return devices[index];
        }

        public static int DefaultIndex()
        {
            var devices = Enumerate(out _);
            var firstGpu = devices.FirstOrDefault(d => d.Kind == DeviceKind.OpenCl);
            return firstGpu?.Index ?? devices.First(d => d.Kind == DeviceKind.MultiCoreOptimized).Index;
        }
    }
}

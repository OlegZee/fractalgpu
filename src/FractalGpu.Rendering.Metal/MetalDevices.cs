using FractalGpu.Rendering.Fractal;
using Metal;

namespace FractalGpu.Rendering.Metal;

/// <summary>Cloo-free description of a Metal GPU, mirroring <c>OpenClDeviceInfo</c>.</summary>
public sealed record MetalDeviceInfo(int Index, string Name, bool LowPower, bool Removable, ulong RecommendedMaxWorkingSetBytes);

/// <summary>
/// Enumerates Metal GPUs and exposes them as <see cref="DeviceDescriptor"/>s that continue the
/// index space of <see cref="DeviceRegistry"/> (which, on Apple mobile platforms, has no OpenCL entries).
/// </summary>
public static class MetalDevices
{
    internal static IReadOnlyList<IMTLDevice> Enumerate()
    {
#if MACOS
        return MTLDevice.GetAllDevices();
#else
        var device = MTLDevice.SystemDefault;
        return device is null ? [] : [device];
#endif
    }

    internal static IMTLDevice GetByIndex(int index)
    {
        var devices = Enumerate();
        if (index < 0 || index >= devices.Count)
            throw new ArgumentException($"Metal device index {index} is out of range (0..{devices.Count - 1}).");
        return devices[index];
    }

    public static IReadOnlyList<MetalDeviceInfo> EnumerateInfo() =>
        Enumerate().Select((d, i) => new MetalDeviceInfo(i, d.Name,
#if MACOS
            d.LowPower, d.Removable,
#else
            false, false,
#endif
            d.RecommendedMaxWorkingSetSize)).ToList();

    /// <summary>
    /// Builds device descriptors for every Metal GPU, numbered from <paramref name="firstIndex"/>
    /// so they can be appended to <see cref="DeviceRegistry.Enumerate"/>'s list.
    /// </summary>
    public static IReadOnlyList<DeviceDescriptor> Describe(int firstIndex)
    {
        var result = new List<DeviceDescriptor>();
        var infos = EnumerateInfo();
        foreach (var info in infos)
        {
            result.Add(new DeviceDescriptor(firstIndex + result.Count, DeviceKind.Metal, info.Name, Details(info),
                () => new LyapRendererMetal(info.Index)));
        }

        // perf variants come after all regular Metal devices, like the OpenCL (perf) entries in DeviceRegistry
        foreach (var info in infos)
        {
            var details = Details(info) + "  fast-math: output statistically equivalent, not pixel-reproducible";
            result.Add(new DeviceDescriptor(firstIndex + result.Count, DeviceKind.MetalPerf, $"{info.Name} (perf)", details,
                () => new LyapRendererMetalPerf(info.Index)));
        }
        return result;
    }

    private static string Details(MetalDeviceInfo info)
    {
        var details = $"(Metal)  mem: {info.RecommendedMaxWorkingSetBytes / (1024 * 1024)} MB";
        if (info.LowPower) details += "  low-power";
        return details;
    }

    /// <summary>CPU devices from <see cref="DeviceRegistry"/> followed by all Metal GPUs.</summary>
    public static IReadOnlyList<DeviceDescriptor> EnumerateAll(out string? openClError)
    {
        var devices = DeviceRegistry.Enumerate(out openClError).ToList();
        devices.AddRange(Describe(devices.Count));
        return devices;
    }
}

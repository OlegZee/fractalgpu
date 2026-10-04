using FractalGpu.Rendering.Benchmarking;
using Foundation;
using UIKit;

namespace FractalGpu.BenchIos;

internal static class DeviceInfo
{
    /// <summary>The shared <see cref="MachineInfo"/> header plus the iOS-specific lines.</summary>
    public static IEnumerable<string> Header()
    {
        var device = UIDevice.CurrentDevice;
        yield return $"FractalGPU bench on {device.Model} ({device.SystemName} {device.SystemVersion})";
        foreach (var line in MachineInfo.Header()) yield return line;
        yield return $"Thermal: {NSProcessInfo.ProcessInfo.ThermalState}";
    }
}

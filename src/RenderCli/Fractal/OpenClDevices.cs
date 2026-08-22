using Cloo;

namespace FractalGpu.RenderCli.Fractal
{
	/// <summary>
	/// Helpers for enumerating and selecting OpenCL platforms/devices.
	/// </summary>
	internal static class OpenClDevices
	{
		public static IReadOnlyList<(ComputePlatform Platform, ComputeDevice Device)> Enumerate()
		{
			OpenClLibraryResolver.Initialize();

			return (from p in ComputePlatform.Platforms
				from d in p.Devices
				select (p, d)).ToList();
		}

		public static (ComputePlatform Platform, ComputeDevice Device) GetByIndex(int index)
		{
			var devices = Enumerate();
			if (devices.Count == 0)
				throw new InvalidOperationException("No OpenCL devices found (GPU/OpenCL drivers missing?)");

			if (index < 0 || index >= devices.Count)
				throw new ArgumentOutOfRangeException(nameof(index),
					$"Device index {index} is out of range (0..{devices.Count - 1}). Run 'list-devices' to see available devices.");

			return devices[index];
		}
	}
}

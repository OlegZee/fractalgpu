using System.Runtime.InteropServices;

namespace FractalGpu.Rendering.Benchmarking
{
    /// <summary>
    /// Describes the machine a benchmark ran on, so that pasted results stay identifiable.
    /// Shared by RenderCli and the mobile bench apps; Apple platforms are queried through sysctl,
    /// Linux through /proc/cpuinfo, Windows through the environment.
    /// </summary>
    public static class MachineInfo
    {
        /// <summary>Hardware model, e.g. "Mac16,7", "iPad8,1", or null when unknown.</summary>
        public static string? Model =>
            OperatingSystem.IsMacOS() ? Sysctl("hw.model")
            : OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsTvOS() ? Sysctl("hw.machine")
            : null;

        /// <summary>CPU brand string, e.g. "Apple M5 Pro", or null when unknown.</summary>
        public static string? Cpu
        {
            get
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
                    return Sysctl("machdep.cpu.brand_string");
                if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                        return line?[(line.IndexOf(':') + 1)..].Trim();
                    }
                    catch (IOException) { return null; }
                }
                if (OperatingSystem.IsWindows())
                    return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                return null;
            }
        }

        public static long TotalMemoryBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        /// <summary>Multi-line header: machine, OS/runtime, date.</summary>
        public static IEnumerable<string> Header()
        {
            var parts = new List<string>();
            if (Model is { } model) parts.Add(model);
            if (Cpu is { } cpu) parts.Add(cpu);
            if (parts.Count == 0) parts.Add(Environment.MachineName);
            parts.Add($"{Environment.ProcessorCount} cores");
            parts.Add($"{TotalMemoryBytes / (1024 * 1024 * 1024)} GB");
            yield return "Machine: " + string.Join(", ", parts);
            yield return $"System:  {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), .NET {Environment.Version}";
            yield return $"Date:    {DateTime.Now:yyyy-MM-dd HH:mm}";
        }

        [DllImport("libc", EntryPoint = "sysctlbyname")]
        private static extern int SysctlByName(string name, IntPtr output, ref nint length, IntPtr newValue, nint newLength);

        private static string? Sysctl(string name)
        {
            try
            {
                nint length = 0;
                if (SysctlByName(name, IntPtr.Zero, ref length, IntPtr.Zero, 0) != 0 || length <= 0) return null;
                var buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (SysctlByName(name, buffer, ref length, IntPtr.Zero, 0) != 0) return null;
                    return Marshal.PtrToStringAnsi(buffer);
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                return null;
            }
        }
    }
}

namespace FractalGpu.Rendering.Metal;

internal static class MetalResources
{
    public static string Lyapunov => GetResourceString("Lyapunov.metal");
    public static string LyapunovPerf => GetResourceString("LyapunovPerf.metal");

    private static string GetResourceString(string name)
    {
        var assembly = typeof(MetalResources).Assembly;
        var resource =
            assembly.GetManifestResourceStream($"FractalGpu.Rendering.Metal.Resources.{name}")
            ?? throw new ArgumentException("Resource with a specified name was not found.", nameof(name));

        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }
}

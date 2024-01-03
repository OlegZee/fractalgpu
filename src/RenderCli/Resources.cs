namespace FractalGpu.RenderCli;

public static class Resources
{
    public static string Lyapunov => GetResourceString("Lyapunov.c");
    // public static string LyapunovNonoptimized => GetResourceString("LyapunovNonoptimized.c");

    private static string GetResourceString(string name)
    {
        var assembly = typeof(Resources).Assembly;
        var resource =
            assembly.GetManifestResourceStream($"FractalGpu.RenderCli.Resources.{name}")
            ?? throw new ArgumentException("Resource with a specified name was not found.", nameof(name));

        using var reader = new System.IO.StreamReader(resource);
        return reader.ReadToEnd();
    }
}
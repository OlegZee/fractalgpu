namespace OlegZee.Fractal;

public static class Resources
{
    public static string Lyapunov => GetResourceString("Lyapunov.c");
    public static string LyapunovNonoptimized => GetResourceString("LyapunovNonoptimized.c");

    private static string GetResourceString(string name)
    {
        var assembly = typeof(Resources).Assembly;
        var resource = assembly.GetManifestResourceStream($"RenderCli.Resources.{name}");
        using var reader = new System.IO.StreamReader(resource);
        return reader.ReadToEnd();
    }
}
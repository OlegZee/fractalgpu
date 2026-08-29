using System.Collections.Generic;

namespace FractalGpu.Browser.Core;

/// <summary>A named starting point: pattern plus the region of (A,B) space worth looking at.</summary>
public sealed record Preset(string Name, string Pattern, FractalView View, int Iterations = 2000)
{
    public override string ToString() => Name;
}

public static class Presets
{
    /// <summary>
    /// The six regions the legacy FractalBrowser shipped as its "fractal type" list, plus the full
    /// 0..4 window. Unlike the legacy presets these are starting points, not the only reachable states.
    /// </summary>
    public static IReadOnlyList<Preset> All { get; } =
    [
        new("Standard", "ab", new FractalView(2, 4, 2, 4)),
        new("Standard (detail)", "ab", new FractalView(2.7, 3.3, 3.6, 4)),
        new("Jellyfish", "bbaba", new FractalView(3.8225, 3.8711, 3.8218, 3.8607), 6000),
        new("Zircon Zity", "bbbbbbaaaaaa", new FractalView(3.4, 4, 2.5, 3.4), 4000),
        new("aabab", "aabab", new FractalView(2, 4, 2, 4)),
        new("aaabbb", "aaabbb", new FractalView(0, 4, 0, 4)),
        new("Full window", "ab", new FractalView(0, 4, 0, 4)),
    ];

    public static Preset Default => All[0];
}

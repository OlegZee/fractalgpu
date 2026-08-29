using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FractalGpu.Browser.Core;

public readonly record struct ColorStop(double Position, byte R, byte G, byte B);

/// <summary>
/// Maps a Lyapunov exponent to a colour.
/// <para>
/// The exponent splits into the two regimes the fractal is about: <b>order</b> (exponent &lt;= 0, the
/// orbit is stable) and <b>chaos</b> (exponent &gt; 0). Each regime has its own 256-entry ramp, indexed
/// exactly the way <c>LyapRendererBase.ColorFromExp</c> indexes its colours — integer truncation of
/// <c>exp(±exponent) * 255</c>, with the contrast gamma applied to the already-quantised value.
/// Reproducing that arithmetic (rather than the mathematically cleaner rounding) is what makes
/// <see cref="Classic"/> byte-identical to the BMP that RenderCli writes.
/// </para>
/// <para>
/// Because the index only has 256 possible values, the contrast curve collapses into a small lookup
/// table built once per colour pass instead of a <c>Math.Pow</c> per pixel.
/// </para>
/// </summary>
public sealed class Palette
{
    private readonly uint[] _order;
    private readonly uint[] _chaos;

    private Palette(string name, Func<int, (byte R, byte G, byte B)> order, Func<int, (byte R, byte G, byte B)> chaos)
    {
        Name = name;
        _order = BuildLut(order);
        _chaos = BuildLut(chaos);
    }

    public string Name { get; }

    public override string ToString() => Name;

    private static uint[] BuildLut(Func<int, (byte R, byte G, byte B)> ramp)
    {
        var lut = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var (r, g, b) = ramp(i);
            lut[i] = Bgra(r, g, b);
        }
        return lut;
    }

    private static uint Bgra(byte r, byte g, byte b) => (uint)(b | (g << 8) | (r << 16) | (0xFF << 24));

    private static readonly uint White = Bgra(255, 255, 255);
    private static readonly uint Black = Bgra(0, 0, 0);

    /// <summary>
    /// Writes the exponent map into a top-down BGRA buffer.
    /// Screen row <c>y</c> comes from <c>map[x, h-1-y]</c>: the map's second index runs along A,
    /// which the renderers emit bottom-up.
    /// </summary>
    public unsafe void Colorize(float[,] map, byte* buffer, int rowBytes, double contrast)
    {
        var w = map.GetLength(0);
        var h = map.GetLength(1);
        var order = _order;
        var chaos = _chaos;
        var gamma = BuildContrastTable(contrast);
        var localBuffer = buffer;

        Parallel.For(0, h, y =>
        {
            var row = (uint*)(localBuffer + (nint)y * rowBytes);
            var j = h - 1 - y;

            for (var x = 0; x < w; x++)
            {
                var e = map[x, j];

                if (float.IsInfinity(e)) { row[x] = Black; continue; }
                if (float.IsNaN(e)) { row[x] = White; continue; }

                row[x] = e > 0
                    ? chaos[ChaosIndex(e)]
                    : order[gamma[OrderIndex(e)]];
            }
        });
    }

    // exp(e) is in (0,1] for e <= 0 and exp(-e) in (0,1) for e > 0, so both indices land in 0..255.
    private static int OrderIndex(double exponent) => Clamp((int)(Math.Exp(exponent) * 255));
    private static int ChaosIndex(double exponent) => 255 - Clamp((int)(Math.Exp(-exponent) * 255));

    private static int Clamp(int value) => value < 0 ? 0 : value > 255 ? 255 : value;

    /// <summary>
    /// The library's gamma: <c>i^contrast / 255^(contrast-1)</c>, truncated and clamped. Only 256
    /// inputs exist, so it is tabulated per colour pass.
    /// </summary>
    private static int[] BuildContrastTable(double contrast)
    {
        var gamma = Math.Max(contrast, 0.01);
        var divisor = Math.Pow(255, gamma - 1);
        var table = new int[256];

        for (var i = 0; i < 256; i++)
            table[i] = Clamp((int)(Math.Pow(i, gamma) / divisor));

        return table;
    }

    /// <summary>Colour of a single exponent, for the cursor read-out.</summary>
    public (byte R, byte G, byte B) Map(double exponent, double contrast)
    {
        if (double.IsInfinity(exponent)) return (0, 0, 0);
        if (double.IsNaN(exponent)) return (255, 255, 255);

        var packed = exponent > 0
            ? _chaos[ChaosIndex(exponent)]
            : _order[BuildContrastTable(contrast)[OrderIndex(exponent)]];

        return ((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
    }

    #region Ramps

    private static ColorStop S(double p, byte r, byte g, byte b) => new(p, r, g, b);

    /// <summary>Linear interpolation across gradient stops, sampled at <c>i/255</c>.</summary>
    private static Func<int, (byte, byte, byte)> Gradient(params ColorStop[] stops) => i =>
    {
        var t = i / 255.0;
        if (t <= stops[0].Position) return (stops[0].R, stops[0].G, stops[0].B);

        for (var k = 1; k < stops.Length; k++)
        {
            var hi = stops[k];
            if (t > hi.Position) continue;

            var lo = stops[k - 1];
            var span = hi.Position - lo.Position;
            var f = span <= 0 ? 0 : (t - lo.Position) / span;
            return ((byte)(lo.R + (hi.R - lo.R) * f),
                    (byte)(lo.G + (hi.G - lo.G) * f),
                    (byte)(lo.B + (hi.B - lo.B) * f));
        }

        var last = stops[^1];
        return (last.R, last.G, last.B);
    };

    /// <summary>
    /// Byte-identical to <c>LyapRendererBase.ColorFromExp</c>: amber for the ordered regime with the
    /// green channel truncated at 0.85 of red, pure blue for the chaotic one.
    /// </summary>
    public static readonly Palette Classic = new("Classic (amber/blue)",
        i => ((byte)i, (byte)(i * .85), 0),
        i => (0, 0, (byte)i));

    public static readonly Palette Ember = new("Ember",
        Gradient(S(0, 8, 0, 12), S(0.35, 128, 17, 12), S(0.65, 224, 96, 8), S(0.85, 252, 190, 62), S(1, 255, 248, 220)),
        Gradient(S(0, 4, 2, 20), S(0.5, 26, 12, 72), S(1, 92, 40, 148)));

    public static readonly Palette Glacier = new("Glacier",
        Gradient(S(0, 4, 8, 24), S(0.4, 16, 76, 130), S(0.75, 92, 186, 220), S(1, 236, 253, 255)),
        Gradient(S(0, 10, 4, 18), S(0.55, 78, 12, 84), S(1, 214, 64, 152)));

    public static readonly Palette Viridis = new("Viridis",
        Gradient(S(0, 68, 1, 84), S(0.25, 59, 82, 139), S(0.5, 33, 145, 140), S(0.75, 94, 201, 98), S(1, 253, 231, 37)),
        Gradient(S(0, 0, 0, 0), S(1, 70, 70, 78)));

    public static readonly Palette Mono = new("Mono",
        Gradient(S(0, 0, 0, 0), S(1, 255, 255, 255)),
        Gradient(S(0, 0, 0, 0), S(1, 90, 90, 90)));

    public static readonly Palette Sunset = new("Sunset",
        Gradient(S(0, 12, 4, 32), S(0.3, 106, 26, 92), S(0.6, 214, 68, 96), S(0.82, 250, 148, 78), S(1, 255, 232, 168)),
        Gradient(S(0, 2, 6, 24), S(0.6, 10, 62, 96), S(1, 34, 148, 160)));

    public static IReadOnlyList<Palette> All { get; } = [Classic, Ember, Glacier, Viridis, Mono, Sunset];

    #endregion
}

using System;
using System.Globalization;
using FractalGpu.Rendering.Common;

namespace FractalGpu.Browser.Core;

/// <summary>
/// The rectangle of Lyapunov parameter space currently shown on screen.
/// B is the horizontal axis (grows to the right), A is the vertical axis (grows upwards),
/// matching <c>LyapRendererCpu.RenderImpl</c>: <c>b = B.Start + i*bscale</c>, <c>a = A.Start + j*ascale</c>,
/// with map row <c>j</c> drawn bottom-up.
/// </summary>
public readonly record struct FractalView(double MinA, double MaxA, double MinB, double MaxB)
{
    public const double MinSpan = 1e-12;

    /// <summary>
    /// Outside 0..4 the logistic step <c>r·x·(1-x)</c> escapes the unit interval and every pixel is a
    /// black divergence marker, so this is the parameter range worth showing.
    /// </summary>
    public const double DomainMin = 0;
    public const double DomainMax = 4;

    public double SpanA => MaxA - MinA;
    public double SpanB => MaxB - MinB;
    public double CenterA => (MinA + MaxA) / 2;
    public double CenterB => (MinB + MaxB) / 2;

    /// <summary>Zoom level relative to the classic full 1..4 window.</summary>
    public double ZoomFactor => 3.0 / Math.Max(SpanB, MinSpan);

    public static FractalView Default => new(1, 4, 1, 4);

    public Range<double> A => new(MinA, MaxA);
    public Range<double> B => new(MinB, MaxB);

    public static FractalView FromRanges(Range<double> a, Range<double> b) => new(a.Start, a.End, b.Start, b.End);

    public static FractalView FromCenter(double centerA, double centerB, double spanA, double spanB) =>
        new(centerA - spanA / 2, centerA + spanA / 2, centerB - spanB / 2, centerB + spanB / 2);

    /// <summary>
    /// Expands the deficient axis so one screen pixel covers the same parameter distance on both
    /// axes; the requested region always stays fully visible (a "fit", never a crop).
    /// </summary>
    public FractalView FitAspect(double pixelWidth, double pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return this;

        var scale = Math.Max(SpanB / pixelWidth, SpanA / pixelHeight);
        var fitted = FromCenter(CenterA, CenterB, scale * pixelHeight, scale * pixelWidth);

        // The widened axis otherwise spills past r = 4 and fills the edge of the window with a band
        // of pure divergence. Sliding it back costs nothing: the slack it slides through is the very
        // space the widening just added, so the requested region stays fully visible.
        var (minA, maxA) = SlideIntoDomain(fitted.MinA, fitted.MaxA, MinA, MaxA);
        var (minB, maxB) = SlideIntoDomain(fitted.MinB, fitted.MaxB, MinB, MaxB);

        return new FractalView(minA, maxA, minB, maxB);
    }

    private static (double Min, double Max) SlideIntoDomain(double min, double max, double requestedMin, double requestedMax)
    {
        // Only tidy up a region that was inside the domain to begin with — someone who deliberately
        // panned past r = 4 should stay where they put themselves.
        if (requestedMin < DomainMin || requestedMax > DomainMax) return (min, max);
        if (max - min > DomainMax - DomainMin) return (min, max);

        if (max > DomainMax) return (min - (max - DomainMax), DomainMax);
        if (min < DomainMin) return (DomainMin, max + (DomainMin - min));

        return (min, max);
    }

    /// <summary>Parameter values under a pixel position; (0,0) is the top-left corner of the viewport.</summary>
    public (double A, double B) ToParams(double px, double py, double pixelWidth, double pixelHeight)
    {
        var b = MinB + px / Math.Max(pixelWidth, 1) * SpanB;
        var a = MaxA - py / Math.Max(pixelHeight, 1) * SpanA;
        return (a, b);
    }

    public (double X, double Y) ToPixels(double a, double b, double pixelWidth, double pixelHeight)
    {
        var x = (b - MinB) / Math.Max(SpanB, MinSpan) * pixelWidth;
        var y = (MaxA - a) / Math.Max(SpanA, MinSpan) * pixelHeight;
        return (x, y);
    }

    /// <summary>Zooms by <paramref name="factor"/> (&gt;1 zooms in) keeping the point under the cursor fixed.</summary>
    public FractalView ZoomAt(double factor, double px, double py, double pixelWidth, double pixelHeight)
    {
        var (anchorA, anchorB) = ToParams(px, py, pixelWidth, pixelHeight);
        var fx = pixelWidth <= 0 ? 0.5 : px / pixelWidth;
        var fy = pixelHeight <= 0 ? 0.5 : py / pixelHeight;

        var spanB = SpanB / factor;
        var spanA = SpanA / factor;

        var minB = anchorB - fx * spanB;
        var maxA = anchorA + fy * spanA;
        return Clamp(new FractalView(maxA - spanA, maxA, minB, minB + spanB));
    }

    public FractalView PanByPixels(double dxPixels, double dyPixels, double pixelWidth, double pixelHeight)
    {
        var db = -dxPixels / Math.Max(pixelWidth, 1) * SpanB;
        var da = dyPixels / Math.Max(pixelHeight, 1) * SpanA;
        return Clamp(new FractalView(MinA + da, MaxA + da, MinB + db, MaxB + db));
    }

    /// <summary>Builds a view from a rubber-band rectangle expressed in viewport pixels.</summary>
    public FractalView FromPixelRect(double x0, double y0, double x1, double y1, double pixelWidth, double pixelHeight)
    {
        var (a0, b0) = ToParams(Math.Min(x0, x1), Math.Max(y0, y1), pixelWidth, pixelHeight);
        var (a1, b1) = ToParams(Math.Max(x0, x1), Math.Min(y0, y1), pixelWidth, pixelHeight);
        return Clamp(new FractalView(a0, a1, b0, b1));
    }

    /// <summary>Keeps spans sane so runaway zoom cannot produce degenerate or NaN ranges.</summary>
    public static FractalView Clamp(FractalView v)
    {
        var spanA = Math.Max(v.SpanA, MinSpan);
        var spanB = Math.Max(v.SpanB, MinSpan);
        if (!double.IsFinite(spanA) || !double.IsFinite(spanB) || !double.IsFinite(v.CenterA) || !double.IsFinite(v.CenterB))
            return Default;

        return FromCenter(v.CenterA, v.CenterB, spanA, spanB);
    }

    public string ToInvariantString() =>
        string.Create(CultureInfo.InvariantCulture, $"{MinA:R};{MaxA:R};{MinB:R};{MaxB:R}");

    public static bool TryParse(string? text, out FractalView view)
    {
        view = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4) return false;

        var values = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return false;

        view = Clamp(new FractalView(values[0], values[1], values[2], values[3]));
        return true;
    }
}

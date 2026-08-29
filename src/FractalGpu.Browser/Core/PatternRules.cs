using System;
using System.Linq;

namespace FractalGpu.Browser.Core;

/// <summary>
/// Validation for the Lyapunov sequence string.
/// The renderers treat <c>'a'</c> as the A parameter and <b>every other character</b> as B, so a typo
/// silently renders a different fractal. The browser rejects it up front instead.
/// </summary>
public static class PatternRules
{
    public const int MaxLength = 32;

    /// <summary>
    /// Normalises to lower case and reports why a pattern is unusable, if it is.
    /// <paramref name="normalized"/> is only meaningful when the method returns true.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = "";
        var trimmed = (input ?? "").Trim();

        if (trimmed.Length == 0)
        {
            error = "Pattern is empty — use letters a and b, e.g. \"ab\".";
            return false;
        }

        if (trimmed.Length > MaxLength)
        {
            // Above 32 the GPU perf path drops its compile-time specialisation, and long patterns
            // stop being visually distinguishable anyway.
            error = $"Pattern is longer than {MaxLength} characters.";
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.Any(c => c != 'a' && c != 'b'))
        {
            error = "Pattern may only contain the letters a and b.";
            return false;
        }

        if (!lower.Contains('a') || !lower.Contains('b'))
        {
            error = "Pattern needs at least one a and one b, otherwise one axis has no effect.";
            return false;
        }

        normalized = lower;
        error = null;
        return true;
    }

    public static bool IsValid(string? input) => TryNormalize(input, out _, out _);
}

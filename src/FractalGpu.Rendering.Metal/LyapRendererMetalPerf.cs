using FractalGpu.Rendering.Fractal;
using Foundation;
using Metal;

namespace FractalGpu.Rendering.Metal;

/// <summary>
/// Metal counterpart of <see cref="LyapRendererOpenClPerf"/> on <c>LyapunovPerf.metal</c>: fast-math,
/// pattern bitmask in registers, one <c>fast::log2</c> per 4 iterations, compile-time pattern specialization
/// through <c>PAT_BITS</c>/<c>PAT_LEN</c>/<c>PHASE0</c> preprocessor macros. Statistically equivalent but
/// not pixel-reproducible against <see cref="LyapRendererMetal"/>, which stays the reference.
/// </summary>
public class LyapRendererMetalPerf(int deviceIndex = 0) : LyapRendererMetal(deviceIndex)
{
    protected override string KernelSource => MetalResources.LyapunovPerf;

    protected override MTLCompileOptions CreateCompileOptions(Lyapunov.Settings settings, int[] mask)
    {
        var options = new MTLCompileOptions { FastMathEnabled = true };
        if (mask.Length <= 32)
        {
            var patBits = 0u;
            for (var k = 0; k < mask.Length; k++)
                patBits |= (uint)(mask[k] != 0 ? 1 : 0) << k;

            options.PreprocessorMacros = new NSDictionary<NSString, NSObject>(
                [new NSString("PAT_BITS"), new NSString("PAT_LEN"), new NSString("PHASE0")],
                [new NSString($"{patBits}u"), new NSNumber(mask.Length), new NSNumber(settings.Warmup % mask.Length)]);
        }
        return options;
    }
}

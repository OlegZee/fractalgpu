using System.Numerics;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// SIMD-vectorized single-core renderer. Packs adjacent pixels along the A axis into
    /// <see cref="Vector{T}"/> lanes (128-bit NEON on ARM64, 256-bit AVX2 on x64, 512-bit
    /// with DOTNET_MaxVectorTBitWidth=512), so one code path covers Windows, Linux and macOS.
    /// Falls back to the scalar implementation for row tails and non-accelerated hardware.
    /// </summary>
    public class LyapRendererSimd : LyapRendererCpu
    {
        public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
        {
            var lanes = Vector<double>.Count;
            if (!Vector.IsHardwareAccelerated || h < lanes)
                return base.RenderImpl(w, h, settings);

            var result = new float[w, h];
            var bscale = (settings.B.End - settings.B.Start) / w;
            var ascale = (settings.A.End - settings.A.Start) / h;

            var patternSize = settings.Pattern.Length;
            var rByPatternIndex0 = new Vector<double>[patternSize];
            var rByPatternIndex1 = new Vector<double>[patternSize];
            var rByPatternIndex2 = new Vector<double>[patternSize];
            var rByPatternIndex3 = new Vector<double>[patternSize];
            var scalarPattern = new double[patternSize];
            Span<double> aLanes = stackalloc double[lanes];

            for (var i = 0; i < w; i++)
            {
                var b = settings.B.Start + i * bscale;
                var bVec = new Vector<double>(b);

                var j = 0;
                for (; j <= h - 4 * lanes; j += 4 * lanes)
                {
                    FillPatternVectors(rByPatternIndex0, settings, j, ascale, aLanes, bVec);
                    FillPatternVectors(rByPatternIndex1, settings, j + lanes, ascale, aLanes, bVec);
                    FillPatternVectors(rByPatternIndex2, settings, j + 2 * lanes, ascale, aLanes, bVec);
                    FillPatternVectors(rByPatternIndex3, settings, j + 3 * lanes, ascale, aLanes, bVec);

                    var (e0, e1, e2, e3) = CalculateExponents4(rByPatternIndex0, rByPatternIndex1, rByPatternIndex2, rByPatternIndex3,
                        settings.InitialValue, settings.Warmup, settings.Iterations);
                    for (var l = 0; l < lanes; l++)
                    {
                        result[i, j + l] = (float)e0[l];
                        result[i, j + lanes + l] = (float)e1[l];
                        result[i, j + 2 * lanes + l] = (float)e2[l];
                        result[i, j + 3 * lanes + l] = (float)e3[l];
                    }
                }
                // Two independent pixel blocks per iteration: the x-update and the log
                // accumulation are serial dependency chains within a block, so a second
                // in-flight block keeps the FP pipeline busy.
                for (; j <= h - 2 * lanes; j += 2 * lanes)
                {
                    FillPatternVectors(rByPatternIndex0, settings, j, ascale, aLanes, bVec);
                    FillPatternVectors(rByPatternIndex1, settings, j + lanes, ascale, aLanes, bVec);

                    var (exp0, exp1) = CalculateExponents2(rByPatternIndex0, rByPatternIndex1,
                        settings.InitialValue, settings.Warmup, settings.Iterations);
                    for (var l = 0; l < lanes; l++)
                    {
                        result[i, j + l] = (float)exp0[l];
                        result[i, j + lanes + l] = (float)exp1[l];
                    }
                }

                for (; j <= h - lanes; j += lanes)
                {
                    FillPatternVectors(rByPatternIndex0, settings, j, ascale, aLanes, bVec);

                    var exponents = CalculateExponents(rByPatternIndex0, settings.InitialValue, settings.Warmup, settings.Iterations);
                    for (var l = 0; l < lanes; l++)
                    {
                        result[i, j + l] = (float)exponents[l];
                    }
                }

                for (; j < h; j++)
                {
                    var a = settings.A.Start + j * ascale;
                    for (var k = 0; k < patternSize; k++)
                    {
                        scalarPattern[k] = settings.Pattern[k] == 'a' ? a : b;
                    }

                    result[i, j] = (float)CalculateExponent(scalarPattern, settings.InitialValue, settings.Warmup, settings.Iterations);
                }
            }

            return result;
        }

        private static void FillPatternVectors(Vector<double>[] rByPatternIndex, Lyapunov.Settings settings,
            int j, double ascale, Span<double> aLanes, Vector<double> bVec)
        {
            for (var l = 0; l < aLanes.Length; l++)
            {
                aLanes[l] = settings.A.Start + (j + l) * ascale;
            }
            var aVec = new Vector<double>(aLanes);

            for (var k = 0; k < rByPatternIndex.Length; k++)
            {
                rByPatternIndex[k] = settings.Pattern[k] == 'a' ? aVec : bVec;
            }
        }

        /// <summary>
        /// Vector counterpart of <see cref="LyapRendererCpu.CalculateExponent"/>: each lane is an
        /// independent pixel. NaN/±Inf are not early-out'ed per lane — they propagate through the
        /// accumulator and yield the same value class the scalar path returns.
        /// </summary>
        private static Vector<double> CalculateExponents(Vector<double>[] pattern, double initial, int warmup, int iterations)
        {
            var x = new Vector<double>(initial);
            var one = Vector<double>.One;
            var two = new Vector<double>(2.0);
            var patternSize = pattern.Length;

            // Same round-up-to-10 loop structure as the scalar version, with the modulo
            // replaced by an incrementally maintained pattern index.
            var k = 0;
            for (var i = 0; i < warmup; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r = pattern[k];
                    if (++k == patternSize) k = 0;
                    x *= r * (one - x);
                }
            }

            var total = Vector<double>.Zero;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r = pattern[k];
                    if (++k == patternSize) k = 0;
                    total += Vector.Log(Vector.Abs(r - two * r * x));
                    x *= r * (one - x);
                }
            }

            return total / new Vector<double>(Math.Log(2) * (iterations - warmup));
        }

        /// <summary>
        /// Same as <see cref="CalculateExponents"/> but iterates two independent pixel blocks
        /// in lockstep so their dependency chains overlap in the FP pipeline.
        /// </summary>
        private static (Vector<double>, Vector<double>) CalculateExponents2(
            Vector<double>[] pattern0, Vector<double>[] pattern1, double initial, int warmup, int iterations)
        {
            var x0 = new Vector<double>(initial);
            var x1 = new Vector<double>(initial);
            var one = Vector<double>.One;
            var two = new Vector<double>(2.0);
            var patternSize = pattern0.Length;

            var k = 0;
            for (var i = 0; i < warmup; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r0 = pattern0[k];
                    var r1 = pattern1[k];
                    if (++k == patternSize) k = 0;
                    x0 *= r0 * (one - x0);
                    x1 *= r1 * (one - x1);
                }
            }

            var total0 = Vector<double>.Zero;
            var total1 = Vector<double>.Zero;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r0 = pattern0[k];
                    var r1 = pattern1[k];
                    if (++k == patternSize) k = 0;
                    total0 += Vector.Log(Vector.Abs(r0 - two * r0 * x0));
                    total1 += Vector.Log(Vector.Abs(r1 - two * r1 * x1));
                    x0 *= r0 * (one - x0);
                    x1 *= r1 * (one - x1);
                }
            }

            var norm = new Vector<double>(Math.Log(2) * (iterations - warmup));
            return (total0 / norm, total1 / norm);
        }

        private static (Vector<double>, Vector<double>, Vector<double>, Vector<double>) CalculateExponents4(
            Vector<double>[] pattern0, Vector<double>[] pattern1, Vector<double>[] pattern2, Vector<double>[] pattern3,
            double initial, int warmup, int iterations)
        {
            var x0 = new Vector<double>(initial);
            var x1 = new Vector<double>(initial);
            var x2 = new Vector<double>(initial);
            var x3 = new Vector<double>(initial);
            var one = Vector<double>.One;
            var two = new Vector<double>(2.0);
            var patternSize = pattern0.Length;

            var k = 0;
            for (var i = 0; i < warmup; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r0 = pattern0[k];
                    var r1 = pattern1[k];
                    var r2 = pattern2[k];
                    var r3 = pattern3[k];
                    if (++k == patternSize) k = 0;
                    x0 *= r0 * (one - x0);
                    x1 *= r1 * (one - x1);
                    x2 *= r2 * (one - x2);
                    x3 *= r3 * (one - x3);
                }
            }

            var total0 = Vector<double>.Zero;
            var total1 = Vector<double>.Zero;
            var total2 = Vector<double>.Zero;
            var total3 = Vector<double>.Zero;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += 10)
            {
                for (var u = 0; u < 10; u++)
                {
                    var r0 = pattern0[k];
                    var r1 = pattern1[k];
                    var r2 = pattern2[k];
                    var r3 = pattern3[k];
                    if (++k == patternSize) k = 0;
                    total0 += Vector.Log(Vector.Abs(r0 - two * r0 * x0));
                    total1 += Vector.Log(Vector.Abs(r1 - two * r1 * x1));
                    total2 += Vector.Log(Vector.Abs(r2 - two * r2 * x2));
                    total3 += Vector.Log(Vector.Abs(r3 - two * r3 * x3));
                    x0 *= r0 * (one - x0);
                    x1 *= r1 * (one - x1);
                    x2 *= r2 * (one - x2);
                    x3 *= r3 * (one - x3);
                }
            }

            var norm = new Vector<double>(Math.Log(2) * (iterations - warmup));
            return (total0 / norm, total1 / norm, total2 / norm, total3 / norm);
        }

        public override string ToString() => $"{nameof(LyapRendererSimd)}[{Vector<double>.Count} lanes]";
    }
}

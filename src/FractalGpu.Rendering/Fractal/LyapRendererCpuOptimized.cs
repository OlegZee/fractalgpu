using System.Numerics;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// SIMD-vectorized single-core renderer. Packs adjacent pixels along the A axis into
    /// <see cref="Vector{T}"/> lanes (128-bit NEON on ARM64, 256-bit AVX2 on x64, 512-bit
    /// with DOTNET_MaxVectorTBitWidth=512), so one code path covers Windows, Linux and macOS.
    ///
    /// Instead of a transcendental log per iteration, the sum of logs is computed as the log
    /// of a running product: every <see cref="GroupSize"/> iterations the product's IEEE-754
    /// exponent is moved into an integer accumulator with bit operations (an exact,
    /// rounding-free renormalization) and the mantissa is reset to [1, 2), so a single
    /// Math.Log2 per pixel remains at the end. Lanes that hit a zero/denormal derivative or
    /// a non-finite value are flagged during renormalization and recomputed with the scalar
    /// code, which also handles row tails and non-accelerated hardware.
    /// </summary>
    public class LyapRendererCpuOptimized : LyapRendererCpu
    {
        private const int GroupSize = 10;
        private const ulong MantissaMask = 0x000F_FFFF_FFFF_FFFF;
        private const ulong OneExponentBits = 0x3FF0_0000_0000_0000; // biased exponent of 1.0
        private const ulong ExponentFieldMax = 0x7FF;                // Inf/NaN exponent field
        private const int ExponentBias = 1023;

        private readonly record struct BlockResult(Vector<double> Mantissa, Vector<ulong> ExponentSum, Vector<ulong> BadLanes);

        public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
        {
            var lanes = Vector<double>.Count;
            if (!Vector.IsHardwareAccelerated || h < lanes)
                return base.RenderImpl(w, h, settings);

            var result = new float[w, h];
            var bscale = (settings.B.End - settings.B.Start) / w;
            var ascale = (settings.A.End - settings.A.Start) / h;

            var patternSize = settings.Pattern.Length;
            var rTabs = new Vector<double>[4][];
            var twoRTabs = new Vector<double>[4][];
            for (var t = 0; t < 4; t++)
            {
                rTabs[t] = new Vector<double>[patternSize];
                twoRTabs[t] = new Vector<double>[patternSize];
            }
            var scalarPattern = new double[patternSize];
            Span<double> aLanes = stackalloc double[lanes];

            for (var i = 0; i < w; i++)
            {
                var b = settings.B.Start + i * bscale;
                var bVec = new Vector<double>(b);

                var j = 0;
                // Four, then two, then one independent pixel block per loop iteration: the
                // x-update and the running product are serial dependency chains within a
                // block, so extra in-flight blocks keep the FP pipeline busy.
                for (; j <= h - 4 * lanes; j += 4 * lanes)
                {
                    for (var t = 0; t < 4; t++)
                        FillPatternVectors(rTabs[t], twoRTabs[t], settings, j + t * lanes, ascale, aLanes, bVec);

                    var (b0, b1, b2, b3, groups) = CalculateExponents4(rTabs, twoRTabs,
                        settings.InitialValue, settings.Warmup, settings.Iterations);
                    StoreBlock(result, i, j, b0, groups, settings, b, ascale, scalarPattern);
                    StoreBlock(result, i, j + lanes, b1, groups, settings, b, ascale, scalarPattern);
                    StoreBlock(result, i, j + 2 * lanes, b2, groups, settings, b, ascale, scalarPattern);
                    StoreBlock(result, i, j + 3 * lanes, b3, groups, settings, b, ascale, scalarPattern);
                }

                for (; j <= h - 2 * lanes; j += 2 * lanes)
                {
                    FillPatternVectors(rTabs[0], twoRTabs[0], settings, j, ascale, aLanes, bVec);
                    FillPatternVectors(rTabs[1], twoRTabs[1], settings, j + lanes, ascale, aLanes, bVec);

                    var (b0, b1, groups) = CalculateExponents2(rTabs, twoRTabs,
                        settings.InitialValue, settings.Warmup, settings.Iterations);
                    StoreBlock(result, i, j, b0, groups, settings, b, ascale, scalarPattern);
                    StoreBlock(result, i, j + lanes, b1, groups, settings, b, ascale, scalarPattern);
                }

                for (; j <= h - lanes; j += lanes)
                {
                    FillPatternVectors(rTabs[0], twoRTabs[0], settings, j, ascale, aLanes, bVec);

                    var (b0, groups) = CalculateExponents(rTabs[0], twoRTabs[0],
                        settings.InitialValue, settings.Warmup, settings.Iterations);
                    StoreBlock(result, i, j, b0, groups, settings, b, ascale, scalarPattern);
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

        private static void FillPatternVectors(Vector<double>[] rByPatternIndex, Vector<double>[] twoRByPatternIndex,
            Lyapunov.Settings settings, int j, double ascale, Span<double> aLanes, Vector<double> bVec)
        {
            for (var l = 0; l < aLanes.Length; l++)
            {
                aLanes[l] = settings.A.Start + (j + l) * ascale;
            }
            var aVec = new Vector<double>(aLanes);
            var two = new Vector<double>(2.0);
            var twoAVec = two * aVec; // doubling is exact, so 2r precomputed matches 2*r computed per step
            var twoBVec = two * bVec;

            for (var k = 0; k < rByPatternIndex.Length; k++)
            {
                var isA = settings.Pattern[k] == 'a';
                rByPatternIndex[k] = isA ? aVec : bVec;
                twoRByPatternIndex[k] = isA ? twoAVec : twoBVec;
            }
        }

        /// <summary>
        /// Moves the product's biased IEEE-754 exponents into the integer accumulator and resets
        /// the mantissas to [1, 2). Exact: no floating-point rounding is involved. Lanes whose
        /// exponent field is 0 (zero or denormal product) or 0x7FF (Inf/NaN) are flagged as bad.
        /// </summary>
        private static void Renormalize(ref Vector<double> p, ref Vector<ulong> exponentSum, ref Vector<ulong> badLanes)
        {
            var bits = Vector.AsVectorUInt64(p);
            var e = Vector.ShiftRightLogical(bits, 52); // sign is always 0: p is a product of absolute values
            exponentSum += e;
            badLanes |= Vector.Equals(e, Vector<ulong>.Zero) | Vector.Equals(e, new Vector<ulong>(ExponentFieldMax));
            p = Vector.AsVectorDouble((bits & new Vector<ulong>(MantissaMask)) | new Vector<ulong>(OneExponentBits));
        }

        /// <summary>
        /// Writes one pixel block. Bad lanes (zero/denormal derivative, Inf/NaN — flagged by
        /// <see cref="Renormalize"/>) are recomputed with the scalar code so special-value
        /// semantics match <see cref="LyapRendererCpu"/> exactly.
        /// </summary>
        private static void StoreBlock(float[,] result, int i, int j, BlockResult block, int groups,
            Lyapunov.Settings settings, double b, double ascale, double[] scalarPattern)
        {
            var n = settings.Iterations - settings.Warmup;
            for (var l = 0; l < Vector<double>.Count; l++)
            {
                if (block.BadLanes[l] != 0)
                {
                    var a = settings.A.Start + (j + l) * ascale;
                    for (var k = 0; k < scalarPattern.Length; k++)
                    {
                        scalarPattern[k] = settings.Pattern[k] == 'a' ? a : b;
                    }

                    result[i, j + l] = (float)CalculateExponent(scalarPattern, settings.InitialValue, settings.Warmup, settings.Iterations);
                }
                else
                {
                    var log2Sum = (long)block.ExponentSum[l] - (long)ExponentBias * groups + Math.Log2(block.Mantissa[l]);
                    result[i, j + l] = (float)(log2Sum / n);
                }
            }
        }

        /// <summary>
        /// Vector counterpart of <see cref="LyapRendererCpu.CalculateExponent"/>: each lane is an
        /// independent pixel; same round-up-to-10 loop structure, with the per-iteration log
        /// replaced by the running product and one exact renormalization per group.
        /// </summary>
        private static (BlockResult, int Groups) CalculateExponents(Vector<double>[] r, Vector<double>[] twoR,
            double initial, int warmup, int iterations)
        {
            var x = new Vector<double>(initial);
            var one = Vector<double>.One;
            var patternSize = r.Length;

            var k = 0;
            for (var i = 0; i < warmup; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv = r[k];
                    if (++k == patternSize) k = 0;
                    x *= rv * (one - x);
                }
            }

            var p = one;
            var eAcc = Vector<ulong>.Zero;
            var bad = Vector<ulong>.Zero;
            var groups = 0;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv = r[k];
                    var tv = twoR[k];
                    if (++k == patternSize) k = 0;
                    p *= Vector.Abs(rv - tv * x);
                    x *= rv * (one - x);
                }
                Renormalize(ref p, ref eAcc, ref bad);
                groups++;
            }

            return (new BlockResult(p, eAcc, bad), groups);
        }

        private static (BlockResult, BlockResult, int Groups) CalculateExponents2(
            Vector<double>[][] rTabs, Vector<double>[][] twoRTabs, double initial, int warmup, int iterations)
        {
            var r0 = rTabs[0];
            var r1 = rTabs[1];
            var t0 = twoRTabs[0];
            var t1 = twoRTabs[1];
            var x0 = new Vector<double>(initial);
            var x1 = x0;
            var one = Vector<double>.One;
            var patternSize = r0.Length;

            var k = 0;
            for (var i = 0; i < warmup; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv0 = r0[k];
                    var rv1 = r1[k];
                    if (++k == patternSize) k = 0;
                    x0 *= rv0 * (one - x0);
                    x1 *= rv1 * (one - x1);
                }
            }

            var p0 = one;
            var p1 = one;
            var e0 = Vector<ulong>.Zero;
            var e1 = Vector<ulong>.Zero;
            var bad0 = Vector<ulong>.Zero;
            var bad1 = Vector<ulong>.Zero;
            var groups = 0;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv0 = r0[k];
                    var tv0 = t0[k];
                    var rv1 = r1[k];
                    var tv1 = t1[k];
                    if (++k == patternSize) k = 0;
                    p0 *= Vector.Abs(rv0 - tv0 * x0);
                    p1 *= Vector.Abs(rv1 - tv1 * x1);
                    x0 *= rv0 * (one - x0);
                    x1 *= rv1 * (one - x1);
                }
                Renormalize(ref p0, ref e0, ref bad0);
                Renormalize(ref p1, ref e1, ref bad1);
                groups++;
            }

            return (new BlockResult(p0, e0, bad0), new BlockResult(p1, e1, bad1), groups);
        }

        private static (BlockResult, BlockResult, BlockResult, BlockResult, int Groups) CalculateExponents4(
            Vector<double>[][] rTabs, Vector<double>[][] twoRTabs, double initial, int warmup, int iterations)
        {
            var r0 = rTabs[0];
            var r1 = rTabs[1];
            var r2 = rTabs[2];
            var r3 = rTabs[3];
            var t0 = twoRTabs[0];
            var t1 = twoRTabs[1];
            var t2 = twoRTabs[2];
            var t3 = twoRTabs[3];
            var x0 = new Vector<double>(initial);
            var x1 = x0;
            var x2 = x0;
            var x3 = x0;
            var one = Vector<double>.One;
            var patternSize = r0.Length;

            var k = 0;
            for (var i = 0; i < warmup; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv0 = r0[k];
                    var rv1 = r1[k];
                    var rv2 = r2[k];
                    var rv3 = r3[k];
                    if (++k == patternSize) k = 0;
                    x0 *= rv0 * (one - x0);
                    x1 *= rv1 * (one - x1);
                    x2 *= rv2 * (one - x2);
                    x3 *= rv3 * (one - x3);
                }
            }

            var p0 = one;
            var p1 = one;
            var p2 = one;
            var p3 = one;
            var e0 = Vector<ulong>.Zero;
            var e1 = Vector<ulong>.Zero;
            var e2 = Vector<ulong>.Zero;
            var e3 = Vector<ulong>.Zero;
            var bad0 = Vector<ulong>.Zero;
            var bad1 = Vector<ulong>.Zero;
            var bad2 = Vector<ulong>.Zero;
            var bad3 = Vector<ulong>.Zero;
            var groups = 0;
            k = warmup % patternSize;
            for (var i = warmup; i < iterations; i += GroupSize)
            {
                for (var u = 0; u < GroupSize; u++)
                {
                    var rv0 = r0[k];
                    var tv0 = t0[k];
                    var rv1 = r1[k];
                    var tv1 = t1[k];
                    var rv2 = r2[k];
                    var tv2 = t2[k];
                    var rv3 = r3[k];
                    var tv3 = t3[k];
                    if (++k == patternSize) k = 0;
                    p0 *= Vector.Abs(rv0 - tv0 * x0);
                    p1 *= Vector.Abs(rv1 - tv1 * x1);
                    p2 *= Vector.Abs(rv2 - tv2 * x2);
                    p3 *= Vector.Abs(rv3 - tv3 * x3);
                    x0 *= rv0 * (one - x0);
                    x1 *= rv1 * (one - x1);
                    x2 *= rv2 * (one - x2);
                    x3 *= rv3 * (one - x3);
                }
                Renormalize(ref p0, ref e0, ref bad0);
                Renormalize(ref p1, ref e1, ref bad1);
                Renormalize(ref p2, ref e2, ref bad2);
                Renormalize(ref p3, ref e3, ref bad3);
                groups++;
            }

            return (new BlockResult(p0, e0, bad0), new BlockResult(p1, e1, bad1),
                new BlockResult(p2, e2, bad2), new BlockResult(p3, e3, bad3), groups);
        }

        public override string ToString() => $"{nameof(LyapRendererCpuOptimized)}[{Vector<double>.Count} lanes, deferred log]";
    }
}

// Metal port of Resources/LyapunovPerf.c from FractalGpu.Rendering (the fast-math GPU path).
// Pattern bitmask in registers, one log2 per 4 iterations with ln2 folded into the output scale,
// compile-time pattern specialization via PAT_BITS / PAT_LEN / PHASE0 preprocessor macros.
#include <metal_stdlib>
using namespace metal;

#define NEXT(x,r) (r * x - r * x * x)

struct LyapunovParams
{
    float initialX;
    int warmupCount;
    int iterationsCount;
    int maskLen;
    float divider;
    uint rowStride;   // floats per output row (= image width)
};

kernel void Lyapunov(
    device const float* b              [[buffer(0)]],
    device const float* a              [[buffer(1)]],
    device float* t                    [[buffer(2)]],
    device const int* m                [[buffer(3)]],
    constant LyapunovParams& p         [[buffer(4)]],
    uint2 gid                          [[thread_position_in_grid]])
{
    const uint i = gid.x;   // group of 4 B-values
    const uint j = gid.y;   // A-row

    float4 x = float4(p.initialX);
    float4 bv = float4(b[i * 4 + 0], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
    float4 av = float4(a[j]);

#ifdef PAT_LEN
    // pattern specialized at pipeline build time
    const uint pat = (uint)(PAT_BITS);
    const int patLen = PAT_LEN;
#else
    // pattern as a private bitmask (maskLen <= 32); bit k set => 'b'
    uint pat = 0u;
    for (int k = 0; k < p.maskLen; k++)
        pat |= (uint)(m[k] != 0) << k;
    const int patLen = p.maskLen;
#endif

    int k = 0;
    for (int idx = 0; idx < p.warmupCount; idx++)
    {
        float4 r = ((pat >> k) & 1u) ? bv : av;
        if (++k == patLen) k = 0;
        x = NEXT(x, r);
    }

#ifdef PHASE0
    k = PHASE0; // warmupCount % patLen, computed on host: makes k compile-time in the hot loop
#endif

    // accumulate the product of |dF| over groups of 4, one log2 per group
    float4 total = float4(0.0f);
    int idx = p.warmupCount;
    for (; idx + 4 <= p.iterationsCount; idx += 4)
    {
        float4 prod = float4(1.0f);
        #pragma unroll
        for (int u = 0; u < 4; u++)
        {
            float4 r = ((pat >> k) & 1u) ? bv : av;
            if (++k == patLen) k = 0;
            prod = prod * fabs(r - r * x * 2.0f);
            x = NEXT(x, r);
        }
        total = total + fast::log2(prod);
    }
    for (; idx < p.iterationsCount; idx++)
    {
        float4 r = ((pat >> k) & 1u) ? bv : av;
        if (++k == patLen) k = 0;
        total = total + fast::log2(fabs(r - r * x * 2.0f));
        x = NEXT(x, r);
    }

    const uint offset = j * p.rowStride + i * 4;
    float4 result = total * (0.6931472f * p.divider);
    t[offset + 0] = result.x;
    t[offset + 1] = result.y;
    t[offset + 2] = result.z;
    t[offset + 3] = result.w;
}

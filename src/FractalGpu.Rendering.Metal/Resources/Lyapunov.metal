// Metal port of Resources/Lyapunov.c from FractalGpu.Rendering (the pixel-reproducible OpenCL reference).
// Same work decomposition: one thread computes four adjacent B-values of one A-row.
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

    for (int idx = 0; idx < p.warmupCount; idx++)
    {
        float4 r = m[idx % p.maskLen] == 0 ? av : bv;
        x = NEXT(x, r);
    }

    float4 total = float4(0.0f);
    for (int idx = p.warmupCount; idx < p.iterationsCount; idx++)
    {
        float4 r = m[idx % p.maskLen] == 0 ? av : bv;
        total = total + fast::log(fabs(r - r * x * 2.0f));
        x = NEXT(x, r);
    }

    const uint offset = j * p.rowStride + i * 4;
    float4 result = total * p.divider;
    t[offset + 0] = result.x;
    t[offset + 1] = result.y;
    t[offset + 2] = result.z;
    t[offset + 3] = result.w;
}

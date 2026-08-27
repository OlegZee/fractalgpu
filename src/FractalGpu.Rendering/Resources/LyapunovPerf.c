#define NEXT(x,r) (r * x - r * x * x)

kernel void Lyapunov(
	__global float* b,
	__global float* a,
	__global /*__write_only*/ float* t,
	__global int * m,
	float initialX,
	int warmupCount, int iterationsCount,
	int maskLen, float divider)
{
	// it calculates vector of 4 values (GPU efficiency)
	int i = get_global_id(0);
	int j = get_global_id(1);

	float4 x = (float4)(initialX);
    float4 bv = vload4(i, b);
    float4 av = (float4)(a[j]);

#ifdef PAT_LEN
	// pattern specialized at program build time
	const uint pat = (uint)(PAT_BITS);
	const int patLen = PAT_LEN;
#else
	// pattern as a private bitmask (maskLen <= 32); bit k set => 'b'
	uint pat = 0u;
	for (int k = 0; k < maskLen; k++)
		pat |= (uint)(m[k] != 0) << k;
	const int patLen = maskLen;
#endif

	int k = 0;
	for (int idx = 0; idx < warmupCount; idx++)
	{
		float4 r = ((pat >> k) & 1u) ? bv : av;
		if (++k == patLen) k = 0;
		x = NEXT(x,r);
	}

#ifdef PHASE0
	k = PHASE0; // warmupCount % patLen, computed on host: makes k compile-time in the hot loop
#endif

	// accumulate the product of |dF| over groups of 4, one native_log2 per group
	float4 total = (float4)(0.0f);
	int idx = warmupCount;
	for (; idx + 4 <= iterationsCount; idx += 4)
	{
		float4 p = (float4)(1.0f);
		#pragma unroll
		for (int u = 0; u < 4; u++)
		{
			float4 r = ((pat >> k) & 1u) ? bv : av;
			if (++k == patLen) k = 0;
			p = p * fabs(r - r * x * 2.0f);
			x = NEXT(x,r);
		}
		total = total + native_log2(p);
	}
	for (; idx < iterationsCount; idx++)
	{
		float4 r = ((pat >> k) & 1u) ? bv : av;
		if (++k == patLen) k = 0;
		total = total + native_log2(fabs(r - r * x * 2.0f));
		x = NEXT(x,r);
	}

    // Store result
    int offset = j * get_global_size(0) + i;
    vstore4(total * (0.6931472f * divider), offset, t);
}

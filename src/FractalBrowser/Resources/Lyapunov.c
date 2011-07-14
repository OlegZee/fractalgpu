#define NEXT(x,r) (r * x - r * x * x)

kernel void Lyapunov(
	constant float* a,
	constant float* b,
	constant float* m,
	global write_only float* t,
	float initialX,
	int warmupCount, int iterationsCount,
	int maskLen, float divider)
{
	int i = get_global_id(0) * 4;
	int j = get_global_id(1);

	int offset = j * get_global_size(0) * 4 + i;

	float4 x = (float4)(initialX);
	float4 bv = (float4)(b[i], b[i+1], b[i+2], b[i+3]);
	float4 av = (float4)(a[j]);
	for (int idx = 0; idx < warmupCount; idx++)
	{
		float4 r = m[idx % maskLen] == 0 ? av : bv;
		x = NEXT(x,r);
	}

	float4 total = (float4)(0.0f);
	for (int idx = warmupCount; idx < iterationsCount; idx++)
	{
		float4 r = m[idx % maskLen] == 0 ? av : bv;
		total = total + native_log(fabs(r - r * x * 2.0f));
		x = NEXT(x,r);
	}

	float4 result = total * divider;
	t[offset]   = result.s0;
	t[offset+1] = result.s1;
	t[offset+2] = result.s2;
	t[offset+3] = result.s3;
}
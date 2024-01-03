#define NEXT(x,r) (r * x - r * x * x)

kernel void Lyapunov(
	__global float* a,
	__global float* b,
	__global /*__write_only*/ float* t,
	__global int * m,
	float initialX,
	int warmupCount, int iterationsCount,
	int maskLen, float divider)
{
	// it calculates vector of 4 values (GPU efficiency)
	int i = get_global_id(0) * 4;
	int j = get_global_id(1);
	
	float4 x = (float4)(initialX);
	float4 av = (float4)(a[i], a[i+1], a[i+2], a[i+3]);
	float4 bv = (float4)(b[j]);
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
	int offset = j * get_global_size(0) * 4 + i;

	t[offset]   = result.s0;
	t[offset+1] = result.s1;
	t[offset+2] = result.s2;
	t[offset+3] = result.s3;
}
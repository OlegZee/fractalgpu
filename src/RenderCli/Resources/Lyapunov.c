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
	int i = get_global_id(0) * 4;
	int j = get_global_id(1);
	
	float4 x = (float4)(initialX);
    float4 bv = vload4(i/4, b);
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

    // Store result
    int offset = j * get_global_size(0) * 4 + i;
    vstore4(total * divider, offset/4, t);
}
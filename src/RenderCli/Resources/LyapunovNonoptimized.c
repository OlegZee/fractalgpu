#define NEXT(x,r) (r * x - r * x * x)

__kernel void LyapunovSimple(
	__global float const * a,
	__global float const * b,
	__global int const * m,
	__global /*__write_only*/ float * t,
	const float initialX,
	const int warmupCount, const int iterationsCount,
	const int maskLen, const float divider,
	const int width)
{
	// it calculates vector of 4 values (GPU efficiency)
	int i = get_global_id(0);
	int j = get_global_id(1);
	
	float x = initialX;
	float av = a[i], bv = b[j];
	for (int idx = 0; idx < warmupCount; idx++)
	{
		float r = m[idx % maskLen] == 0 ? av : bv;
		x = NEXT(x,r);
	}

	float total = 0.0f;
	for (int idx = warmupCount; idx < iterationsCount; idx++)
	{
		float r = m[idx % maskLen] == 0 ? av : bv;
		total = total + native_log(fabs(r - r * x * 2.0f));
		x = NEXT(x,r);
	}

	t[j * width + i]   = total * divider;
}
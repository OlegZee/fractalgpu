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
	int i = get_global_id(0);
	int j = get_global_id(1);

	int offset = j * get_global_size(0) + i;

	float x = initialX;
	float bv = b[i];
	float av = a[j];
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

	t[offset] = total * divider;
}
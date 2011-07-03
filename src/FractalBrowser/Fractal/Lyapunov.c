kernel void Lyapunov(
	constant float* a,
	constant float* b,
	constant float* m,
	global write_only float* t,
	float initialX,
	int warmupCount,
	int iterationsCount,
	int maskLen)
{
	int i = get_global_id(0);
	int j = get_global_id(1);
	float x = initialX;

	float bv = b[i];
	float av = a[j];
	for (int idx = 0; idx < warmupCount; idx++)
	{
		float r = m[idx % maskLen] == 0 ? av : bv;
		x = r * x - r * x * x;
	}

	float total = 0;
	for (int idx = warmupCount; idx < iterationsCount; idx++)
	{
		float r = m[idx % maskLen] == 0 ? av : bv;
		total = total + log(fabs(r - r * x * 2));
		x = r * x - r * x * x;
	}

	int columns = get_global_size(0);
	t[j * columns + i] = total / (iterationsCount - warmupCount);
}
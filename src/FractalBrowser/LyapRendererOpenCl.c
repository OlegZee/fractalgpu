kernel void Lyapunov(
	global read_only float* a,
	global read_only float* b,
	global read_only float* m,
	global write_only float* t,
	float initialX,
	int warmupCount, int maskLen, int iterationsCount, int columns, float divider)
{
	int i = get_global_id(0);
	int j = get_global_id(1);
	float x = initialX;
	float r = 0;
	float bv = b[i];
	float av = a[j];
	for (int idx = 0; idx < warmupCount; idx++)
	{
		r = m[idx % maskLen] == 0 ? av : bv;
		x = r * x - r * x * x;
	}

	float total = (float)0;
	for (int idx = warmupCount; idx < iterationsCount; idx++)
	{
		r = m[idx % maskLen] == 0 ? av : bv;
		total = total + native_log(fabs(r - r * x * 2));
		x = r * x - r * x * x;
	}

	t[j * columns + i] = total * divider;
}
namespace FractalGpu.RenderCli.Common
{
	/// <summary>
	/// Defines the range of some values
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public readonly struct Range<T> where T:struct
	{
		public Range(T start, T end) : this()
		{
			Start = start;
			End = end;
		}

		/// <summary>
		/// Start value
		/// </summary>
		public T Start { get; }

		/// <summary>
		/// End value
		/// </summary>
		public T End { get; }
	}
}
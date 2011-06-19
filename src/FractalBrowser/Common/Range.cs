namespace OlegZee.FractalBrowser.Common
{
	/// <summary>
	/// Defines the range of some values
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public struct Range<T> where T:struct
	{
		private readonly T _start, _end;

		public Range(T start, T end) : this()
		{
			_start = start;
			_end = end;
		}

		/// <summary>
		/// Start value
		/// </summary>
		public T Start { get { return _start; } }

		/// <summary>
		/// End value
		/// </summary>
		public T End { get { return _end; } }
	}
}
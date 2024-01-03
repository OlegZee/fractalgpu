namespace FractalGpu.RenderCli.Common
{
	/// <summary>
	/// Defines Size object
	/// </summary>
	public readonly struct Sz
	{
		public Sz(double width, double height) : this()
		{
			Width = width;
			Height = height;
		}

		public double Width { get; }
		public double Height { get; }
	}
}

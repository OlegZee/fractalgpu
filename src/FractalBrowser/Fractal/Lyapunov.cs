using System.Drawing;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Lyapunov fractals rendering implementation
	/// </summary>
	public partial class Lyapunov
	{
		// TODO separate visualization from calculation

		public class Settings
		{
			public Sz Size;
			public double Contrast;
			public Palette Palette;

			public FractalSettings Fractal;
		}

		public class Palette
		{
			private Color BaseColor { get; set; }
		}
	}
}

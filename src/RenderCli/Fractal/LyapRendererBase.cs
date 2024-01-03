using FractalGpu.RenderCli.Media;

namespace FractalGpu.RenderCli.Fractal
{
	/// <summary>
	/// CPU based renderer implementation
	/// </summary>
	public abstract class LyapRendererBase : FractalRenderer<Lyapunov.Settings>
	{
		protected const double Contrast = 1.7;

		public override RawBitmap Render(Lyapunov.Settings settings)
		{
			return Render(settings, d => ColorFromExp(d, (float) settings.Contrast));
		}

		#region Implementation

		private static RawColor ColorFromExp(float lyapExp, float contrast)
		{
			if (double.IsNegativeInfinity(lyapExp) || Double.IsPositiveInfinity(lyapExp))
			{
				return RawColor.Black;
			}
			if (double.IsNaN(lyapExp))
			{
				return RawColor.White;
			}
			if (lyapExp > 0)
			{
				var colorIntensity = (int)(Math.Exp(-lyapExp) * 255);
				return RawColor.FromArgb(255, 0, 0, (byte)(255 - colorIntensity));
			}
			else
			{
				var colorIntensity = (int) (Math.Exp(lyapExp)*255);
				colorIntensity = (int) (Math.Pow(colorIntensity, contrast)/Math.Pow(255, (contrast - 1)));

				colorIntensity = Math.Min(Math.Max(colorIntensity, 0), 255);

				return RawColor.FromArgb(255, (byte)colorIntensity, (byte) (colorIntensity*.85), 0);
			}
		}

		#endregion
	}
}
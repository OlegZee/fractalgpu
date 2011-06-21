using System;
using System.Drawing;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// CPU based renderer implementation
	/// </summary>
	public abstract class LyapRendererBase : FractalRenderer<Lyapunov.Settings>
	{
		protected const double Contrast = 1.7;

		public override Bitmap Render(Lyapunov.Settings settings)
		{
			return Render(settings, d => ColorFromExp(d, settings.Contrast));
		}

		#region Implementation

		private static Color ColorFromExp(double lyapExp, double contrast)
		{
			int colorIntensity;
			if (Double.IsNegativeInfinity(lyapExp) || Double.IsPositiveInfinity(lyapExp))
			{
				return Color.Black;
			}
			if (Double.IsNaN(lyapExp))
			{
				return Color.White;
			}
			if (lyapExp > 0)
			{
				colorIntensity = (int)(Math.Exp(-lyapExp) * 255);
				return Color.FromArgb(255, 0, 0, 255 - colorIntensity);
			}

			colorIntensity = (int)(Math.Exp(lyapExp) * 255);
			colorIntensity = (int)(Math.Pow(colorIntensity, contrast) / Math.Pow(255, (contrast - 1)));

			if (colorIntensity > 255)
				colorIntensity = 255;
			else if (colorIntensity < 0)
				colorIntensity = 0;

			return Color.FromArgb(255, colorIntensity, (int)(colorIntensity * .85), 0);
		}

		#endregion
	}
}
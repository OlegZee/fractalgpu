using System;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	public static class Lyapunov
	{
		/// <summary>
		/// Defines fractal settings
		/// </summary>
		public sealed class Settings:RenderSettings
		{
			public double InitialValue { get; private set; }
			public Range<double> A { get; private set; }
			public Range<double> B { get; private set; }
			public string Pattern { get; private set; }

			// View settings
			public double Contrast { get; private set; }

			public int Warmup { get; private set; }
			public int Iterations { get; private set; }

			public Settings()
			{
				A = new Range<double>(1, 4);
				B = new Range<double>(1, 4);
				InitialValue = 0.5;
				Pattern = "ab";
				Warmup = 10;
				Iterations = 100;
				Contrast = 2;
			}

			// TODO validation
			// TODO pattern as bool array

			public Settings SetA(double a1, double a2) {return SetA(new Range<double>(a1, a2));}
			public Settings SetA(Range<double> a) { return Update<Settings>(s => s.A = a); }

			public Settings SetB(Range<double> b) { return Update<Settings>(s => s.B = b); }
			public Settings SetB(double b1, double b2) { return SetB(new Range<double>(b1, b2)); }
			
			public Settings SetInitial(double initial) { return Update<Settings>(s => s.InitialValue = initial); }
			public Settings SetPattern(string pattern) { return Update<Settings>(s => s.Pattern = pattern); }
			public Settings SetContrast(double contrast) { return Update<Settings>(s => s.Contrast = contrast); }
			public Settings SetIterations(int warmup, int iter)
			{
				return Update<Settings>(s => { s.Warmup = warmup; s.Iterations = iter; });
			}

			public new Settings SetSize(Sz sz) { return (Settings)base.SetSize(sz); }

		}
	}
}

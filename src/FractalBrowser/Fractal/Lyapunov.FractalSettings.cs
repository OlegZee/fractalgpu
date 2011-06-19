using System;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	partial class Lyapunov
	{
		/// <summary>
		/// Defines fractal settings
		/// </summary>
		public class FractalSettings
		{
			public double InitialValue { get; private set; }
			public Range<double> A { get; private set; }
			public Range<double> B { get; private set; }
			public string Pattern { get; private set; }

			public int Warmup { get; private set; }
			public int Iterations { get; private set; }

			public FractalSettings(Range<double> a, Range<double> b, string pattern)
			{
				A = a;
				B = b;
				InitialValue = 0.5;
				Pattern = pattern;
			}

			// TODO validation
			// TODO pattern as bool array

			public FractalSettings SetA(Range<double> a) { return NewWith(s => s.A = a); }
			public FractalSettings SetB(Range<double> b) { return NewWith(s => s.B = b); }
			public FractalSettings SetInitial(double initial) { return NewWith(s => s.InitialValue = initial); }
			public FractalSettings SetPattern(string pattern) { return NewWith(s => s.Pattern = pattern); }
			public FractalSettings SetIterations(int warmup, int iter)
			{
				return NewWith(s => {s.Warmup = warmup; s.Iterations = iter;});
			}

			private FractalSettings NewWith(Action<FractalSettings> setter)
			{
				var newSettings = (FractalSettings)MemberwiseClone();
				setter(newSettings);
				return newSettings;
			}
		}
	}
}

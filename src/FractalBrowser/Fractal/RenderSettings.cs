using System;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Fractal rendering settings
	/// </summary>
	public abstract class RenderSettings
	{
		public Sz Size { get; private set; }

		public RenderSettings SetSize(Sz sz) { return Update<RenderSettings>(s => s.Size = sz); }

		protected T Update<T>(Action<T> setter) where T:RenderSettings
		{
			var newSettings = (T)MemberwiseClone();
			setter(newSettings);
			return newSettings;
		}
	}
}

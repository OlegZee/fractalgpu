using System;

namespace OlegZee.FractalBrowser.Common
{
	/// <summary>
	/// Defines Size object
	/// </summary>
	public struct Sz
	{
		private readonly double _width, _height;

		public Sz(double width, double height) : this()
		{
			_width = width;
			_height = height;
		}

		public double Width { get { return _width; } }
		public double Height { get { return _height; } }
	}
}

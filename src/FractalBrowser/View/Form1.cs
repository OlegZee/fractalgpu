using System;
using System.Windows.Forms;
using OlegZee.FractalBrowser.Common;
using OlegZee.FractalBrowser.Fractal;

namespace OlegZee.FractalBrowser.View
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void btnStart_Click(object sender, System.EventArgs e)
		{
			var fractalSettings = new Lyapunov.FractalSettings(new Range<double>(2, 4), new Range<double>(2, 4), "ab")
				.SetInitial(0.5)
				.SetIterations(50, 500);

			var settings = new Lyapunov.Settings {Contrast = 1.7, Size = new Sz(400, 400), Fractal = fractalSettings};
			pictureBox1.Image = null;
			Update();

			//var renderer = new LyapRendererCpu();
			var renderer = new LyapRendererMulticore();
			var startTime = DateTime.Now;
			var bmp = renderer.Render(settings);

			labelRenderTime.Text = "Rendering time: " + (DateTime.Now - startTime).ToString();
			pictureBox1.Image = bmp;
		}
	}
}

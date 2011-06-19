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

		private void Form1_Load(object sender, EventArgs e)
		{
			comboBoxRenderer.Items.Add(new TaggedListItem("CPU", new LyapRendererCpu()));
			comboBoxRenderer.Items.Add(new TaggedListItem("Multicore", new LyapRendererMulticore()));
			comboBoxRenderer.SelectedIndex = 1;
		}

		private class TaggedListItem
		{
			private readonly string _item;
			public readonly object Tag;

			public TaggedListItem(string item, object tag)
			{
				_item = item;
				Tag = tag;
			}

			public override string ToString()
			{
				return _item;
			}
		}

		private void btnStart_Click(object sender, System.EventArgs e)
		{
			var settings = new Lyapunov.Settings()
				.SetA(new Range<double>(1, 5))
				.SetB(new Range<double>(1, 5))
				.SetPattern("ab")
				.SetInitial(0.5)
				.SetIterations(50, 500)

				.SetSize(new Sz(400, 400))
				.SetContrast(1.7);

			pictureBox1.Image = null;
			Update();

			var renderer = (FractalRenderer<Lyapunov.Settings>) (((TaggedListItem) comboBoxRenderer.SelectedItem).Tag);
			var startTime = DateTime.Now;
			var bmp = renderer.Render(settings);

			Log("Rendering time: " + (DateTime.Now - startTime));
			pictureBox1.Image = bmp;
		}

		private void Log(string text)
		{
			listBoxLog.Items.Add(text);
		}

		private void buttonBenchmark_Click(object sender, EventArgs e)
		{
			var settings = new Lyapunov.Settings()
				.SetA(new Range<double>(2, 4))
				.SetB(new Range<double>(2, 4))
				.SetPattern("ab")
				.SetInitial(0.5)
				.SetIterations(50, 500)

				.SetSize(new Sz(400, 400));

			foreach (var renderer in new[] {new LyapRendererCpu(), new LyapRendererMulticore()})
			{
				var startTime = DateTime.Now;
				renderer.RenderImpl(400, 400, settings);

				Log(string.Format("{0} time: {1}", renderer.GetType().Name, (DateTime.Now - startTime)));
				listBoxLog.Update();
			}
		}
	}
}

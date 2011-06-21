using System;
using System.Linq;
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
			comboBoxRenderer.Items.Add(new TaggedListItem("Multicore CPU", new LyapRendererMulticore<LyapRendererCpu>(8)));
			comboBoxRenderer.Items.Add(new TaggedListItem("Multicore GPU", new LyapRendererMulticore<LyapRendererGpu>(2)));
			comboBoxRenderer.Items.Add(new TaggedListItem("GPU", new LyapRendererGpu()));
			comboBoxRenderer.SelectedIndex = comboBoxRenderer.Items.Count - 1;

			
			comboboxPicSize.Items.AddRange(
				new[] { 256, 400, 512, 1024, 2048, 4096 }
				.Select(i => new TaggedListItem(string.Format("{0}x{0}", i), i)).ToArray()
				);

			comboboxPicSize.SelectedIndex = 0;
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
			var picSize = (int)(((TaggedListItem)comboboxPicSize.SelectedItem).Tag);

			var settings = new Lyapunov.Settings()
				.SetA(new Range<double>(1, 5))
				.SetB(new Range<double>(1, 5))
				.SetPattern("ab")
				.SetInitial(0.5)
				.SetIterations(20, 300)

				.SetSize(new Sz(picSize, picSize))
				.SetContrast(1.7);

			pictureBox1.SizeMode = picSize < pictureBox1.Width ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.StretchImage;
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
			listBoxLog.Update();
		}

		private void buttonBenchmark_Click(object sender, EventArgs e)
		{
			var settings = new Lyapunov.Settings()
				.SetA(new Range<double>(2, 4))
				.SetB(new Range<double>(2, 4))
				.SetPattern("ab")
				.SetInitial(0.5)
				.SetIterations(50, 500);

			Log("Benchmark started");

			foreach (var picSize in new[] { 100, 512, 2048 })
			{
				settings = settings.SetSize(new Sz(picSize, picSize));

				Log(string.Format("Testing picture size is {0}x{0}", picSize));

				foreach (var renderer in new LyapRendererBase[]
					{
						new LyapRendererCpu(), new LyapRendererMulticore<LyapRendererCpu>(), new LyapRendererGpu()
					})
				{
					var startTime = DateTime.Now;
					renderer.RenderImpl(picSize, picSize, settings);

					Log(string.Format("{0} time: {1}", renderer.GetType().Name, (DateTime.Now - startTime)));
				}
			}

			Log("Benchmark completed.");

		}
	}
}

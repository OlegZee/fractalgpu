using System;
using System.Collections.Generic;
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

		private enum SelectItem {None, First, Last}

		private static void FillCombo<T>(ComboBox combo, Dictionary<string,T> dictionary, SelectItem selItem)
		{
			combo.Items.AddRange(dictionary.Select(arg => new TaggedListItem(arg.Key, arg.Value)).ToArray());
			switch(selItem)
			{
				case SelectItem.First: combo.SelectedIndex = 0; break;
				case SelectItem.Last: combo.SelectedIndex = combo.Items.Count - 1; break;
			}
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			var renderers = new Dictionary<string,object>
				{
					{"CPU", new LyapRendererCpu()},
					{"Multicore CPU4", new LyapRendererMulticore<LyapRendererCpu>(4)},
					{"Multicore CPU16", new LyapRendererMulticore<LyapRendererCpu>(16)},
					{"GPU (Accelerator.dll)", new LyapRendererGpuMsAccelerator()},
					{"GPU OpenCL/Cloo", new LyapRendererOpenCl()}
				};

			var sizes = new[] { 256, 400, 512, 768, 1024, 1536, 2048, 4096 }
				.ToDictionary(i1 => string.Format("{0}x{0}", i1), i2 => i2);

			var fractalTypes = new Dictionary<string, Func<Lyapunov.Settings, Lyapunov.Settings>>
				{
					{"Standard", s => s.SetPattern("ab").SetA(2, 4).SetB(2, 4)},
					{"Zircon Zity", s => s.SetPattern("bbbbbbaaaaaa").SetA(3.4, 4).SetB(2.5, 3.4)}
				};

			FillCombo(comboBoxRenderer, renderers, SelectItem.Last);
			FillCombo(comboboxPicSize, sizes, SelectItem.First);
			FillCombo(comboBoxFractalType, fractalTypes, SelectItem.First);

			trackBar1.Value = trackBar1.Maximum;
			textBox1.Text = GetIterationsCount().ToString();
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
			var picSize = GetPictureSize();

			var settings = new Lyapunov.Settings()
				.SetA(new Range<double>(2, 4))
				.SetB(new Range<double>(2, 4))
				.SetPattern("ab")
				.SetInitial(0.5)
				.SetIterations(GetIterationsCount()/10, GetIterationsCount())

				.SetSize(new Sz(picSize, picSize))
				.SetContrast(1.7);

			settings = GetApplyFractalType()(settings);

			pictureBox1.SizeMode = picSize < pictureBox1.Width ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.StretchImage;
			pictureBox1.Image = null;
			Update();

			var startTime = DateTime.Now;
			var bmp = GetRenderer().Render(settings);

			var execTime = DateTime.Now - startTime;
			var perf = settings.Size.Width * settings.Size.Height * settings.Iterations/1024/1024/execTime.TotalSeconds;

			Log(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
				execTime.TotalSeconds, settings.Pattern, settings.Iterations,
				settings.Size.Width, settings.Size.Height, comboBoxRenderer.SelectedItem, perf));

			pictureBox1.Image = bmp;
		}

		private void buttonClearLog_Click(object sender, EventArgs e)
		{
			listBoxLog.Items.Clear();
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

			foreach (var picSize in new[] { 100, 512, 1536 })
			{
				settings = settings.SetSize(new Sz(picSize, picSize));

				Log(string.Format("Testing picture size is {0}x{0}", picSize));

				foreach (var renderer in new LyapRendererBase[]
					{
						new LyapRendererCpu(), new LyapRendererMulticore<LyapRendererCpu>(),
						new LyapRendererGpuMsAccelerator(), new LyapRendererOpenCl()
					})
				{
					var startTime = DateTime.Now;
					renderer.RenderImpl(picSize, picSize, settings);

					Log(string.Format("{0} time: {1}", renderer.GetType().Name, (DateTime.Now - startTime)));
				}
			}

			Log("Benchmark completed.");

		}

		private void trackBar1_Scroll(object sender, EventArgs e)
		{
			textBox1.Text = GetIterationsCount().ToString();
		}

		private int GetIterationsCount()
		{
			var value = (int) Math.Pow(10, (double)trackBar1.Value/100);
			var accuracy = value < 200 ? 20 : value < 1000 ? 50 : 100;

			return value /accuracy * accuracy;
		}

		private Func<Lyapunov.Settings,Lyapunov.Settings> GetApplyFractalType()
		{
			var setSettings = (Func<Lyapunov.Settings, Lyapunov.Settings>)(((TaggedListItem)comboBoxFractalType.SelectedItem).Tag);
			return setSettings;
		}

		private int GetPictureSize()
		{
			var picSize = (int)(((TaggedListItem)comboboxPicSize.SelectedItem).Tag);
			return picSize;
		}

		private FractalRenderer<Lyapunov.Settings> GetRenderer()
		{
			var renderer = (FractalRenderer<Lyapunov.Settings>)(((TaggedListItem)comboBoxRenderer.SelectedItem).Tag);
			return renderer;
		}

		private void Log(string text)
		{
			listBoxLog.Items.Add(text);
			listBoxLog.Update();
		}

	}
}

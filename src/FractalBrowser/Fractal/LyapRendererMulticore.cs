using System;
using System.Threading;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	/// <summary>
	/// Multicore CPU renderer implementation
	/// </summary>
	internal class LyapRendererMulticore<TBaseRenderer> : LyapRendererBase where TBaseRenderer : LyapRendererBase, new()
	{
		public LyapRendererMulticore() : this(8)
		{
		}

		public LyapRendererMulticore(int tileCount)
		{
			SplitTilesCount = tileCount;
		}

		private readonly int SplitTilesCount;

		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var coreRenderer = new TBaseRenderer();

			var result = new double[w, h];
			var handles = new AutoResetEvent[SplitTilesCount];

			for(var tileIndex = 0; tileIndex < SplitTilesCount; tileIndex++)
			{
				var tileHeight = h/SplitTilesCount;
				var tileStart = tileHeight*tileIndex;
				// handle error for last tile
				if(tileIndex == SplitTilesCount - 1)
					tileHeight = h - tileHeight*(SplitTilesCount - 1);

				var bPerTile = (settings.B.End - settings.B.Start)/SplitTilesCount;
				var b = settings.B.Start + tileIndex*bPerTile;
				var tileSettings = settings.SetB(new Range<double>(b, b + bPerTile));

				handles[tileIndex] = new AutoResetEvent(false);

				ThreadPool.QueueUserWorkItem(state =>
					{
						var tileResult = coreRenderer.RenderImpl(w, tileHeight, tileSettings);

						// copy result, don't care about threading since no conflicts
						for (var i = 0; i < w; i++)
						for (var j = 0; j < tileHeight; j++)
						{
							result[i, j + tileStart] = tileResult[i, j];
						}

						handles[(int)state].Set();
					}, tileIndex);
			}

			foreach (var autoResetEvent in handles)
			{
				autoResetEvent.WaitOne();
			}

			return result;
		}
	}
}
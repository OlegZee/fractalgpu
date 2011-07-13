using System.Threading;
using OlegZee.FractalBrowser.Common;

namespace OlegZee.FractalBrowser.Fractal
{
	// TODO change to universal multicore renderer
	/// <summary>
	/// Multicore CPU renderer implementation
	/// </summary>
	internal class LyapRendererMulticore<TBaseRenderer> : LyapRendererBase
		where TBaseRenderer : LyapRendererBase, new()
	{
		public LyapRendererMulticore() : this(8)
		{
		}

		public LyapRendererMulticore(int tileCount)
		{
			_splitTilesCount = tileCount;
		}

		private readonly int _splitTilesCount;

		public override double[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
		{
			var coreRenderer = new TBaseRenderer();

			var result = new double[w, h];
			var handles = new AutoResetEvent[_splitTilesCount];

			for(var tileIndex = 0; tileIndex < _splitTilesCount; tileIndex++)
			{
				var tileHeight = h/_splitTilesCount;
				var tileStart = tileHeight*tileIndex;
				// handle error for last tile
				if(tileIndex == _splitTilesCount - 1)
					tileHeight = h - tileHeight*(_splitTilesCount - 1);

				var bPerTile = (settings.A.End - settings.A.Start)/_splitTilesCount;
				var a = settings.A.Start + tileIndex*bPerTile;
				var tileSettings = settings.SetA(new Range<double>(a, a + bPerTile));

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
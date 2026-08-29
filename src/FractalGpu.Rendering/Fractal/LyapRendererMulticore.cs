using FractalGpu.Rendering.Common;

namespace FractalGpu.Rendering.Fractal
{
    // TODO change to universal multicore renderer
    /// <summary>
    /// Multicore CPU renderer implementation
    /// </summary>
    public class LyapRendererMulticore<TBaseRenderer> : LyapRendererBase
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

        public override float[,] RenderImpl(int w, int h, Lyapunov.Settings settings)
        {
            var coreRenderer = new TBaseRenderer();

            var result = new float[w, h];
            // Never more tiles than rows, otherwise the tail tiles are empty.
            var tileCount = Math.Clamp(_splitTilesCount, 1, Math.Max(h, 1));
            var handles = new AutoResetEvent[tileCount];

            // Each tile must reproduce the single-core mapping a = A.Start + j * ascale for its own
            // rows, so the sub-range is derived from the tile's actual row bounds. Splitting the A
            // range into equal parts instead breaks whenever h is not a multiple of the tile count:
            // the last tile keeps the leftover rows but only 1/tileCount of the range.
            var ascale = (settings.A.End - settings.A.Start) / h;

            for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                var tileStart = (int)((long)h * tileIndex / tileCount);
                var tileHeight = (int)((long)h * (tileIndex + 1) / tileCount) - tileStart;

                var a = settings.A.Start + tileStart * ascale;
                var tileSettings = settings with { A = new Range<double>(a, a + tileHeight * ascale) };

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

                        handles[(int)state!].Set();
                    }, tileIndex);
            }

            foreach (var autoResetEvent in handles)
            {
                autoResetEvent.WaitOne();
            }

            return result;
        }

        public override string ToString() => $"LyapRendererMulticore<{typeof(TBaseRenderer).Name}>[{_splitTilesCount} tiles]";
    }
}
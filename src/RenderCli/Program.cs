using FractalGpu.RenderCli.Common;
using FractalGpu.RenderCli.Fractal;

Console.WriteLine("fractalgpu benchmark");

void Render()
{
    var picSize = 2048;
    var iterations = 20000;

    var settings = new Lyapunov.Settings()
        .SetA(new Range<double>(2, 4))
        .SetB(new Range<double>(2, 4))
        .SetPattern("ab")
        .SetInitial(0.5)
        .SetIterations(iterations/10, iterations)

        .SetSize(new Sz(picSize, picSize))
        .SetContrast(1.7);

    var startTime = DateTime.Now;
    Func<LyapRendererBase> makeRenderer = 2 switch
    {
        0 => () => new LyapRendererCpu(),
        1 => () => new LyapRendererMulticore<LyapRendererCpu>(10),
        2 => () => new LyapRendererOpenCl(),
        _ => throw new ArgumentException("Unknown renderer type")
    };
    var renderer = makeRenderer();
    var bmp = renderer.Render(settings);

    var execTime = DateTime.Now - startTime;
    var perf = settings.Size.Width * settings.Size.Height * settings.Iterations/1024/1024/execTime.TotalSeconds;

    Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
        execTime.TotalSeconds, settings.Pattern, settings.Iterations,
        settings.Size.Width, settings.Size.Height, renderer, perf));

    // pictureBox1.Image = bmp;
    bmp.Save("testN.bmp");
}

Render();

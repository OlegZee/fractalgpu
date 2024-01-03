using OlegZee.FractalBrowser.Common;
using OlegZee.FractalBrowser.Fractal;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var devices = (from p in Cloo.ComputePlatform.Platforms
    from d in p.Devices
    select (p, d)).ToArray();
foreach (var (p, d) in devices)
{
    Console.WriteLine($"Platform: {p.Name}, device: {d.Name}");
}
Console.WriteLine(devices.Length == 0 ? "NO DEVICES FOUND" : $"Devices list is OK");


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

    // pictureBox1.SizeMode = picSize < pictureBox1.Width ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.StretchImage;
    // pictureBox1.Image = null;
    // Update();

    var startTime = DateTime.Now;
    var renderer = 2 switch
    {
        0 => (LyapRendererBase) new LyapRendererCpu(),
        1 => new LyapRendererMulticore<LyapRendererCpu>(10),
        2 => new LyapRendererOpenCl(),
        3 => new LyapRendererOpenClNew(),
    };
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

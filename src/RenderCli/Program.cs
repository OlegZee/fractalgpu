using FractalGpu.RenderCli.Common;
using FractalGpu.RenderCli.Fractal;

void Render(LyapRendererBase renderer, string? fileName)
{
    var picSize = 256;
    var iterations = 10000;

    var settings = new Lyapunov.Settings()
        .SetA(new Range<double>(2, 4))
        .SetB(new Range<double>(2, 4))
        .SetPattern("ab")
        .SetInitial(0.5)
        .SetIterations(iterations/10, iterations)

        .SetSize(new Sz(picSize, picSize))
        .SetContrast(1.7);

    var startTime = DateTime.Now;
    var bmp = renderer.Render(settings);

    var execTime = DateTime.Now - startTime;
    var perf = settings.Size.Width * settings.Size.Height * settings.Iterations/1024/1024/execTime.TotalSeconds;

    Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
        execTime.TotalSeconds, settings.Pattern, settings.Iterations,
        settings.Size.Width, settings.Size.Height, renderer, perf));

    if(!string.IsNullOrEmpty(fileName)) bmp.Save(fileName);
}

void Benchmark(Renderer rendererType)
{
    var renderer = makeRenderer(rendererType);
    var picSize = 256;
    var numIterations = 1000;
    
    var settings = new Lyapunov.Settings()
        .SetA(new Range<double>(2, 4))
        .SetB(new Range<double>(2, 4))
        .SetPattern("ab")
        .SetInitial(0.5)
        .SetContrast(1.7);

    var steps = new[]
    {
        () => { picSize = 256; numIterations = 1000; }, 
        () => { picSize = 512; },
        () => { picSize = 1024; }, 
        () => { numIterations = 2500; }, 
        () => { numIterations = 5000; }, 
        () => { numIterations = 10000; }, 
        () => { numIterations = 25000; }, 
        () => { numIterations = 50000; }, 
        () => { picSize = 1536; }, 
        () => { picSize = 2048; }, 
        () => { picSize = 4096; }, 
    };
    
    TimeSpan execTime;
    var stepIndex = 0;
    do
    {
        steps[stepIndex]();
        settings = settings
            .SetIterations(numIterations / 10, numIterations)
            .SetSize(new Sz(picSize, picSize));

        var startTime = DateTime.Now;
        var bmp = renderer.Render(settings);

        execTime = DateTime.Now - startTime;
        var perf = settings.Size.Width * settings.Size.Height * settings.Iterations / 1024 / 1024 /
                   execTime.TotalSeconds;

        Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
            execTime.TotalSeconds, settings.Pattern, settings.Iterations,
            settings.Size.Width, settings.Size.Height, renderer, perf));
        stepIndex++;
    } while (execTime.TotalSeconds < 2.5 && stepIndex < steps.Length);
}

LyapRendererBase makeRenderer(Renderer r) => r switch
{
    Renderer.SingleCpu => new LyapRendererCpu(),
    Renderer.MultiCore => new LyapRendererMulticore<LyapRendererCpu>(256),
    Renderer.Gpu => new LyapRendererOpenCl(),
    _ => throw new ArgumentException("Unknown renderer type")
};

Console.WriteLine("fractalgpu benchmark");
// var renderer = makeRenderer(Renderer.Gpu);
// Render(renderer, "testN.bmp");

Console.WriteLine("Single-core tests\n=================");
Benchmark(Renderer.SingleCpu);
Console.WriteLine("Multi-core tests\n=================");
Benchmark(Renderer.MultiCore);
Console.WriteLine("GPU tests\n=================");
Benchmark(Renderer.Gpu);

public enum Renderer
{
    SingleCpu,
    MultiCore,
    Gpu
}
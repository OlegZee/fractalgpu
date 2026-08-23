using System.CommandLine;

using FractalGpu.Rendering.Common;
using FractalGpu.Rendering.Fractal;

void Render(LyapRendererBase renderer, string? fileName)
{
    var picSize = 256;
    var iterations = 10000;

    var settings = new Lyapunov.Settings
    {
        A = new Range<double>(2, 4),
        B = new Range<double>(2, 4),
        Pattern = "ab",
        InitialValue = 0.5,
        Warmup = iterations / 10,
        Iterations = iterations,
        Size = new Sz(picSize, picSize),
        Contrast = 1.7,
    };

    var startTime = DateTime.Now;
    var bmp = renderer.Render(settings);

    var execTime = DateTime.Now - startTime;
    var perf = settings.Size.Width * settings.Size.Height * settings.Iterations/1024/1024/execTime.TotalSeconds;

    Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
        execTime.TotalSeconds, settings.Pattern, settings.Iterations,
        settings.Size.Width, settings.Size.Height, renderer, perf));

    if(!string.IsNullOrEmpty(fileName)) bmp.Save(fileName);
}

void Benchmark(DeviceDescriptor device)
{
    var renderer = device.CreateRenderer();
    var picSize = 256;
    var numIterations = 1000;
    
    var settings = new Lyapunov.Settings
    {
        A = new Range<double>(2, 4),
        B = new Range<double>(2, 4),
        Pattern = "ab",
        InitialValue = 0.5,
        Contrast = 1.7,
    };

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
        settings = settings with { Warmup = numIterations / 10, Iterations = numIterations, Size = new Sz(picSize, picSize) };

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

void PrintDeviceTable()
{
    var devices = DeviceRegistry.Enumerate(out var openClError);

    foreach (var device in devices)
    {
        var line = $"[{device.Index}] {device.Name}";
        if (!string.IsNullOrEmpty(device.Details)) line += "  " + device.Details;
        Console.WriteLine(line);
    }

    if (openClError != null)
        Console.WriteLine($"OpenCL enumeration failed: {openClError} (CPU devices still available)");
}

var deviceOption = new Option<int?>("--device", "-d")
    { Description = "Device index from 'list-devices' (default: first GPU, else multi-core CPU)" };

var benchmarkCommand = new Command("benchmark", "Run the escalating render benchmark on a selected device");
benchmarkCommand.Options.Add(deviceOption);
benchmarkCommand.SetAction(parseResult =>
{
    var index = parseResult.GetValue(deviceOption) ?? DeviceRegistry.DefaultIndex();

    DeviceDescriptor device;
    try { device = DeviceRegistry.GetByIndex(index); }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message} Run 'list-devices' to see available devices.");
        return 1;
    }

    try
    {
        Console.WriteLine($"fractalgpu benchmark on [{device.Index}] {device.Name}");
        Benchmark(device);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
});

var listDevicesCommand = new Command("list-devices", "List all available render devices (CPU modes and OpenCL devices) with their indexes");
listDevicesCommand.SetAction(_ =>
{
    PrintDeviceTable();
    return 0;
});

var rootCommand = new RootCommand("FractalGPU RenderCli — Lyapunov fractal rendering and benchmarking");
rootCommand.Subcommands.Add(benchmarkCommand);
rootCommand.Subcommands.Add(listDevicesCommand);
rootCommand.SetAction(_ =>
{
    rootCommand.Parse("--help").Invoke();
    return 0;
});

return rootCommand.Parse(args).Invoke();

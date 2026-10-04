using System.CommandLine;

using FractalGpu.Rendering.Benchmarking;
using FractalGpu.Rendering.Common;
using FractalGpu.Rendering.Fractal;

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

var deviceOption = new Option<int[]>("--device", "-d")
{
    Description = "Device index from 'list-devices'; repeatable (-d 0 -d 2) or space-separated (-d 0 2). Default: all devices",
    AllowMultipleArgumentsPerToken = true,
};

var benchmarkCommand = new Command("benchmark", "Run the escalating render benchmark on a selected device");
benchmarkCommand.Options.Add(deviceOption);
benchmarkCommand.SetAction(parseResult =>
{
    var requested = parseResult.GetValue(deviceOption) ?? [];

    List<DeviceDescriptor> devices;
    if (requested.Length == 0)
    {
        devices = DeviceRegistry.Enumerate(out var openClError).ToList();
        if (openClError != null)
            Console.WriteLine($"OpenCL enumeration failed: {openClError} (CPU devices still available)");
    }
    else
    {
        devices = [];
        foreach (var index in requested.Distinct())
        {
            try { devices.Add(DeviceRegistry.GetByIndex(index)); }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message} Run 'list-devices' to see available devices.");
                return 1;
            }
        }
    }

    foreach (var line in MachineInfo.Header()) Console.WriteLine(line);
    Console.WriteLine();

    var results = new List<BenchResult>();
    var anyFailed = false;
    foreach (var device in devices)
    {
        Console.WriteLine($"fractalgpu benchmark on [{device.Index}] {device.Name}");
        if (!string.IsNullOrEmpty(device.Details)) Console.WriteLine($"  {device.Details}");
        try
        {
            var result = Benchmark.Run(device, Console.WriteLine);
            Console.WriteLine(Benchmark.FormatBest(result));
            results.Add(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            anyFailed = true;
        }
        Console.WriteLine();
    }

    if (results.Count > 1)
    {
        foreach (var line in Benchmark.FormatSummary(results))
            Console.WriteLine(line);
    }

    return anyFailed ? 1 : 0;
});

var renderDeviceOption = new Option<int>("--device", "-d")
{
    Description = "Device index from 'list-devices'. Default: preferred device",
    DefaultValueFactory = _ => DeviceRegistry.DefaultIndex(),
};
var outputOption = new Option<string>("--output", "-o")
{
    Description = "Output BMP file path",
    DefaultValueFactory = _ => "fractal.bmp",
};
var sizeOption = new Option<int>("--size")
{
    Description = "Image size in pixels (square)",
    DefaultValueFactory = _ => 512,
};
var iterationsOption = new Option<int>("--iterations")
{
    Description = "Number of iterations per pixel (warmup is iterations/10)",
    DefaultValueFactory = _ => 10000,
};
var patternOption = new Option<string>("--pattern")
{
    Description = "Lyapunov sequence pattern",
    DefaultValueFactory = _ => "ab",
};

var renderCommand = new Command("render", "Render a Lyapunov fractal to a BMP file on a selected device");
renderCommand.Options.Add(renderDeviceOption);
renderCommand.Options.Add(outputOption);
renderCommand.Options.Add(sizeOption);
renderCommand.Options.Add(iterationsOption);
renderCommand.Options.Add(patternOption);
renderCommand.SetAction(parseResult =>
{
    var deviceIndex = parseResult.GetValue(renderDeviceOption);
    var output = parseResult.GetValue(outputOption)!;
    var picSize = parseResult.GetValue(sizeOption);
    var iterations = parseResult.GetValue(iterationsOption);
    var pattern = parseResult.GetValue(patternOption)!;

    DeviceDescriptor device;
    try { device = DeviceRegistry.GetByIndex(deviceIndex); }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message} Run 'list-devices' to see available devices.");
        return 1;
    }

    var settings = new Lyapunov.Settings
    {
        A = new Range<double>(2, 4),
        B = new Range<double>(2, 4),
        Pattern = pattern,
        InitialValue = 0.5,
        Warmup = iterations / 10,
        Iterations = iterations,
        Size = new Sz(picSize, picSize),
        Contrast = 1.7,
    };

    try
    {
        var renderer = device.CreateRenderer();
        Console.WriteLine($"fractalgpu render on [{device.Index}] {device.Name} @{renderer}");

        var startTime = DateTime.Now;
        var bmp = renderer.Render(settings);
        var execTime = DateTime.Now - startTime;

        bmp.Save(output);

        Console.WriteLine(string.Format("Rendering time: {0:#0.000}s '{1}' N{2} {3}x{4}",
            execTime.TotalSeconds, settings.Pattern, settings.Iterations,
            settings.Size.Width, settings.Size.Height));
        Console.WriteLine($"Saved to {output}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    return 0;
});

var listDevicesCommand = new Command("list-devices", "List all available render devices (CPU modes and OpenCL devices) with their indexes");
listDevicesCommand.SetAction(_ =>
{
    PrintDeviceTable();
    return 0;
});

var rootCommand = new RootCommand("FractalGPU RenderCli — Lyapunov fractal rendering and benchmarking");
rootCommand.Subcommands.Add(benchmarkCommand);
rootCommand.Subcommands.Add(renderCommand);
rootCommand.Subcommands.Add(listDevicesCommand);
rootCommand.SetAction(_ =>
{
    rootCommand.Parse("--help").Invoke();
    return 0;
});

return rootCommand.Parse(args).Invoke();

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FractalGpu.Browser.Core;

/// <summary>
/// What the browser remembers between launches. The legacy app forgot everything on every start,
/// including which device worked on this machine.
/// </summary>
public sealed class AppState
{
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 820;

    /// <summary>Stored by name, not index: device indices shift when a driver appears or disappears.</summary>
    public string? DeviceName { get; set; }

    public string Pattern { get; set; } = "ab";
    public string PaletteName { get; set; } = "Classic (amber/blue)";
    public double Contrast { get; set; } = 1.7;
    public double InitialValue { get; set; } = 0.5;
    public int Iterations { get; set; } = 2000;
    public int Warmup { get; set; } = 200;
    public string? View { get; set; }
    public bool SmoothScaling { get; set; } = true;
    public bool ShowPanel { get; set; } = true;

    [JsonIgnore]
    public FractalView ViewOrDefault =>
        FractalView.TryParse(View, out var v) ? v : Presets.Default.View;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "FractalGpu", "browser-state.json");

    public static AppState Load()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path)) return new AppState();

            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(path)) ?? new AppState();
        }
        catch (Exception)
        {
            // A corrupt or unreadable state file must never stop the app from starting.
            return new AppState();
        }
    }

    public void Save()
    {
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception)
        {
            // Persisting preferences is best-effort.
        }
    }
}

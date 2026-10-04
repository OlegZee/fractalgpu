using System.Text;
using FractalGpu.Rendering.Benchmarking;
using FractalGpu.Rendering.Fractal;
using FractalGpu.Rendering.Metal;
using UIKit;

namespace FractalGpu.BenchIos;

/// <summary>
/// Runs the shared escalating benchmark over every CPU mode and every Metal GPU, streams the log into a
/// monospaced text view and lets the result be copied to the clipboard as plain text.
/// </summary>
public sealed class BenchViewController : UIViewController
{
    private readonly UITextView _log = new()
    {
        Editable = false,
        Font = UIFont.GetMonospacedSystemFont(11, UIFontWeight.Regular),
        AlwaysBounceVertical = true,
        TranslatesAutoresizingMaskIntoConstraints = false,
    };

    private readonly StringBuilder _text = new();
    private UIBarButtonItem _runButton = null!;
    private UIBarButtonItem _copyButton = null!;
    private bool _running;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "FractalGPU Bench";
        View!.BackgroundColor = UIColor.SystemBackground;

        _runButton = new UIBarButtonItem("Run", UIBarButtonItemStyle.Done, (_, _) => Run());
        _copyButton = new UIBarButtonItem("Copy", UIBarButtonItemStyle.Plain, (_, _) => CopyLog());
        NavigationItem.RightBarButtonItems = [_runButton, _copyButton];

        View.AddSubview(_log);
        NSLayoutConstraint.ActivateConstraints(
        [
            _log.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            _log.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
            _log.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
            _log.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
        ]);

        foreach (var line in DeviceInfo.Header()) Append(line);
        Append("");
        Append("Devices:");
        var devices = MetalDevices.EnumerateAll(out var openClError);
        foreach (var device in devices)
            Append($"[{device.Index}] {device.Name}" + (device.Details.Length > 0 ? "  " + device.Details : ""));
        if (openClError != null) Append($"(OpenCL unavailable: {openClError})");
        Append("");
        Append("Tap Run to start the full benchmark on every device. Keep the device plugged in and cool.");
    }

    private void Run()
    {
        if (_running) return;
        _running = true;
        _runButton.Enabled = false;
        UIApplication.SharedApplication.IdleTimerDisabled = true;

        _text.Clear();
        _log.Text = "";
        foreach (var line in DeviceInfo.Header()) Append(line);
        Append("");

        Task.Run(() =>
        {
            var results = new List<BenchResult>();
            foreach (var device in MetalDevices.EnumerateAll(out _))
            {
                Append($"fractalgpu benchmark on [{device.Index}] {device.Name}");
                if (device.Details.Length > 0) Append($"  {device.Details}");
                try
                {
                    var result = Benchmark.Run(device, Append);
                    Append(Benchmark.FormatBest(result));
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    Append($"Error: {ex.Message}");
                }
                Append("");
            }

            foreach (var line in Benchmark.FormatSummary(results)) Append(line);
            Append("");
            Append($"Thermal at the end: {Foundation.NSProcessInfo.ProcessInfo.ThermalState}");

            InvokeOnMainThread(() =>
            {
                _running = false;
                _runButton.Enabled = true;
                UIApplication.SharedApplication.IdleTimerDisabled = false;
            });
        });
    }

    private void CopyLog()
    {
        UIPasteboard.General.String = _text.ToString();
        var alert = UIAlertController.Create("Copied", "Benchmark text copied to the clipboard.", UIAlertControllerStyle.Alert);
        PresentViewController(alert, true, null);
        Task.Delay(900).ContinueWith(_ => InvokeOnMainThread(() => alert.DismissViewController(true, null)));
    }

    private void Append(string line)
    {
        lock (_text) _text.AppendLine(line);
        InvokeOnMainThread(() =>
        {
            _log.Text = _text.ToString();
            var end = new Foundation.NSRange(_log.Text.Length - 1, 1);
            _log.ScrollRangeToVisible(end);
        });
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FractalGpu.Browser.Core;
using FractalGpu.Browser.Views;
using FractalGpu.Rendering.Fractal;

namespace FractalGpu.Browser.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>
    /// How long the view must sit still before a render is queued. Long enough that a drag or a burst
    /// of wheel notches produces one render, short enough to feel immediate.
    /// </summary>
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(110);

    /// <summary>Above this the render cost stops being worth the extra sharpness on a HiDPI screen.</summary>
    private const int MaxRenderPixels = 4_200_000;

    private readonly AppState _state = AppState.Load();
    private readonly RenderService _service = new();
    private readonly NavigationHistory _history = new();
    private readonly DispatcherTimer _debounce;

    private FractalCanvas? _canvas;
    private Window? _window;
    private bool _suppressRender;

    public MainWindowViewModel()
    {
        Devices = Devices_Enumerate();
        _selectedDevice = Devices.FirstOrDefault(d => d.Name == _state.DeviceName)
                          ?? Devices.FirstOrDefault(d => d.Index == Core.Devices.DefaultIndex())
                          ?? Devices[0];

        _selectedPalette = Palette.All.FirstOrDefault(p => p.Name == _state.PaletteName) ?? Palette.Classic;
        _pattern = PatternRules.IsValid(_state.Pattern) ? _state.Pattern : "ab";
        _contrast = _state.Contrast;
        _initialValue = _state.InitialValue;
        _iterations = Math.Max(_state.Iterations, 100);
        _warmup = Math.Clamp(_state.Warmup, 0, _iterations - 1);
        _smoothScaling = _state.SmoothScaling;
        _showPanel = _state.ShowPanel;

        _debounce = new DispatcherTimer { Interval = Debounce };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RenderNow(); };

        _service.Completed += OnRenderCompleted;
        _service.Failed += message => Dispatcher.UIThread.Post(() => ErrorText = message);
        _service.BusyChanged += busy => Dispatcher.UIThread.Post(() => IsBusy = busy);

        BackCommand = new RelayCommand(GoBack, () => _history.CanGoBack);
        ForwardCommand = new RelayCommand(GoForward, () => _history.CanGoForward);
        ResetViewCommand = new RelayCommand(() => NavigateTo(SelectedPreset?.View ?? Presets.Default.View));
        ZoomInCommand = new RelayCommand(() => ZoomBy(2));
        ZoomOutCommand = new RelayCommand(() => ZoomBy(0.5));
        SaveImageCommand = new RelayCommand(() => _ = SaveImageAsync());
        GoToViewCommand = new RelayCommand(ApplyViewText);

        if (Core.Devices.OpenClError is { } error)
            ErrorText = $"OpenCL unavailable: {error}";
    }

    private static IReadOnlyList<DeviceDescriptor> Devices_Enumerate()
    {
        try { return Core.Devices.All; }
        catch (Exception) { return [DeviceRegistry.GetByIndex(0)]; }
    }

    #region Wiring

    /// <summary>Called from the window once the canvas exists.</summary>
    public void Attach(FractalCanvas canvas, Window window)
    {
        _canvas = canvas;
        _window = window;

        canvas.ViewChanged += OnCanvasViewChanged;
        canvas.CursorMoved += OnCursorMoved;
        canvas.CursorLeft += () => CursorText = "";
        canvas.SmoothScaling = SmoothScaling;

        canvas.SetView(_state.ViewOrDefault);
        _history.Push(new Scene(canvas.View, Pattern));
        RaiseHistoryCommands();
        RequestRender();
    }

    public void PersistState(Window window)
    {
        _state.WindowWidth = window.Width;
        _state.WindowHeight = window.Height;
        _state.DeviceName = SelectedDevice?.Name;
        _state.Pattern = Pattern;
        _state.PaletteName = SelectedPalette.Name;
        _state.Contrast = Contrast;
        _state.InitialValue = InitialValue;
        _state.Iterations = Iterations;
        _state.Warmup = Warmup;
        _state.SmoothScaling = SmoothScaling;
        _state.ShowPanel = ShowPanel;
        _state.View = _canvas?.View.ToInvariantString();
        _state.Save();
    }

    public double InitialWindowWidth => _state.WindowWidth;
    public double InitialWindowHeight => _state.WindowHeight;

    #endregion

    #region Bound state

    public IReadOnlyList<DeviceDescriptor> Devices { get; }
    public IReadOnlyList<Palette> Palettes { get; } = Palette.All;
    public IReadOnlyList<Preset> PresetList { get; } = Presets.All;

    private DeviceDescriptor? _selectedDevice;
    public DeviceDescriptor? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetField(ref _selectedDevice, value)) return;
            OnPropertyChanged(nameof(DeviceDetails));
            _service.ResetTimingHeuristic();
            RequestRender();
        }
    }

    public string DeviceDetails => SelectedDevice?.Details ?? "";

    private Palette _selectedPalette;
    public Palette SelectedPalette
    {
        get => _selectedPalette;
        set { if (SetField(ref _selectedPalette, value)) Recolor(); }
    }

    private Preset? _selectedPreset;
    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetField(ref _selectedPreset, value) || value is null) return;

            Pattern = value.Pattern;
            Iterations = value.Iterations;
            Warmup = Math.Max(10, value.Iterations / 10);
            NavigateTo(value.View);
        }
    }

    private string _pattern;
    public string Pattern
    {
        get => _pattern;
        set
        {
            if (!SetField(ref _pattern, value)) return;

            if (PatternRules.TryNormalize(value, out var normalized, out var error))
            {
                PatternError = null;
                _effectivePattern = normalized;
                RequestRender();
            }
            else
            {
                PatternError = error;
            }
        }
    }

    private string _effectivePattern = "ab";

    private string? _patternError;
    public string? PatternError
    {
        get => _patternError;
        private set { if (SetField(ref _patternError, value)) OnPropertyChanged(nameof(HasPatternError)); }
    }

    public bool HasPatternError => _patternError is not null;

    private int _iterations;
    public int Iterations
    {
        get => _iterations;
        set
        {
            var clamped = Math.Clamp(value, 20, 500_000);
            if (!SetField(ref _iterations, clamped)) return;

            OnPropertyChanged(nameof(IterationsLog));
            if (_warmup >= clamped) Warmup = Math.Max(0, clamped - 1);
            RequestRender();
        }
    }

    /// <summary>
    /// Log scale for the slider: iteration count spans three orders of magnitude, and the interesting
    /// detail changes multiplicatively, not additively.
    /// </summary>
    public double IterationsLog
    {
        get => Math.Log10(Math.Max(_iterations, 20));
        set => Iterations = Quantize((int)Math.Round(Math.Pow(10, value)));
    }

    private static int Quantize(int value)
    {
        var step = value < 200 ? 20 : value < 1000 ? 50 : value < 20000 ? 100 : 1000;
        return Math.Max(step, value / step * step);
    }

    private int _warmup;
    public int Warmup
    {
        get => _warmup;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, _iterations - 1));
            if (SetField(ref _warmup, clamped)) RequestRender();
        }
    }

    private double _initialValue;
    public double InitialValue
    {
        get => _initialValue;
        set { if (SetField(ref _initialValue, Math.Clamp(value, 0.001, 0.999))) RequestRender(); }
    }

    private double _contrast;
    public double Contrast
    {
        get => _contrast;
        // Contrast only shapes the colour ramp, so it never needs a new fractal — just a recolour.
        set { if (SetField(ref _contrast, Math.Clamp(value, 0.2, 6))) Recolor(); }
    }

    private bool _smoothScaling;
    public bool SmoothScaling
    {
        get => _smoothScaling;
        set
        {
            if (!SetField(ref _smoothScaling, value)) return;
            if (_canvas is not null) _canvas.SmoothScaling = value;
        }
    }

    private bool _showPanel;
    public bool ShowPanel
    {
        get => _showPanel;
        set => SetField(ref _showPanel, value);
    }

    private int _exportScale = 2;
    public int ExportScale
    {
        get => _exportScale;
        set => SetField(ref _exportScale, Math.Clamp(value, 1, 8));
    }

    #endregion

    #region Status

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private string _cursorText = "";
    public string CursorText
    {
        get => _cursorText;
        private set => SetField(ref _cursorText, value);
    }

    private string _zoomText = "";
    public string ZoomText
    {
        get => _zoomText;
        private set => SetField(ref _zoomText, value);
    }

    private string _viewText = "";
    public string ViewText
    {
        get => _viewText;
        set => SetField(ref _viewText, value);
    }

    private string? _errorText;
    public string? ErrorText
    {
        get => _errorText;
        private set { if (SetField(ref _errorText, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorText);

    private string? _precisionWarning;
    public string? PrecisionWarning
    {
        get => _precisionWarning;
        private set { if (SetField(ref _precisionWarning, value)) OnPropertyChanged(nameof(HasPrecisionWarning)); }
    }

    public bool HasPrecisionWarning => _precisionWarning is not null;

    #endregion

    #region Commands

    public RelayCommand BackCommand { get; }
    public RelayCommand ForwardCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand SaveImageCommand { get; }
    public RelayCommand GoToViewCommand { get; }

    private void RaiseHistoryCommands()
    {
        BackCommand.RaiseCanExecuteChanged();
        ForwardCommand.RaiseCanExecuteChanged();
    }

    private void GoBack()
    {
        if (_history.TryGoBack(out var scene)) RestoreScene(scene);
    }

    private void GoForward()
    {
        if (_history.TryGoForward(out var scene)) RestoreScene(scene);
    }

    private void RestoreScene(Scene scene)
    {
        if (_canvas is null) return;

        _suppressRender = true;
        Pattern = scene.Pattern;
        _suppressRender = false;

        _canvas.SetView(scene.View);
        UpdateViewLabels(_canvas.View);
        RaiseHistoryCommands();
        RequestRender();
    }

    private void ZoomBy(double factor)
    {
        if (_canvas is null) return;

        var view = _canvas.View.ZoomAt(factor, _canvas.Bounds.Width / 2, _canvas.Bounds.Height / 2,
            _canvas.Bounds.Width, _canvas.Bounds.Height);
        NavigateTo(view);
    }

    private void NavigateTo(FractalView view)
    {
        if (_canvas is null) return;

        _canvas.SetView(view);
        _history.Push(new Scene(_canvas.View, _effectivePattern));
        RaiseHistoryCommands();
        UpdateViewLabels(_canvas.View);
        RequestRender();
    }

    private void ApplyViewText()
    {
        if (!FractalView.TryParse(ViewText, out var view))
        {
            ErrorText = "Coordinates must be four numbers: minA;maxA;minB;maxB";
            return;
        }

        ErrorText = null;
        NavigateTo(view);
    }

    #endregion

    #region Rendering

    private void OnCanvasViewChanged(FractalView view, bool gestureFinished)
    {
        if (gestureFinished)
        {
            _history.Push(new Scene(view, _effectivePattern));
            RaiseHistoryCommands();
        }

        UpdateViewLabels(view);
        RequestRender();
    }

    private void UpdateViewLabels(FractalView view)
    {
        ZoomText = view.ZoomFactor >= 1000
            ? string.Create(CultureInfo.InvariantCulture, $"zoom {view.ZoomFactor:0.###e+0}x")
            : string.Create(CultureInfo.InvariantCulture, $"zoom {view.ZoomFactor:0.##}x");

        ViewText = view.ToInvariantString();
    }

    private void RequestRender()
    {
        if (_suppressRender || _canvas is null) return;

        _debounce.Stop();
        _debounce.Start();
    }

    private void RenderNow()
    {
        if (_canvas is null) return;

        var (job, renderWidth, renderHeight) = BuildJob();
        if (job is null) return;

        UpdatePrecisionWarning(job.View, renderWidth);
        _ = renderHeight;
        _service.Request(job);
    }

    /// <summary>
    /// Builds the job for the current viewport. The pixel grid is rounded up to a multiple of 16
    /// because the OpenCL kernel consumes four B-values per work item and splits rows into power-of-two
    /// chunks; ragged sizes silently drop the trailing columns or rows. The view is widened by the same
    /// proportion so the extra pixels carry real data and the canvas simply clips them.
    /// </summary>
    private (RenderJob? Job, int Width, int Height) BuildJob()
    {
        if (_canvas is null || SelectedDevice is null) return (null, 0, 0);

        var bounds = _canvas.Bounds;
        if (bounds.Width < 4 || bounds.Height < 4) return (null, 0, 0);

        var scaling = Math.Clamp(TopLevel.GetTopLevel(_canvas)?.RenderScaling ?? 1.0, 1.0, 2.0);
        var pixelWidth = bounds.Width * scaling;
        var pixelHeight = bounds.Height * scaling;

        var overshoot = pixelWidth * pixelHeight / MaxRenderPixels;
        if (overshoot > 1)
        {
            var shrink = Math.Sqrt(overshoot);
            pixelWidth /= shrink;
            pixelHeight /= shrink;
        }

        var width = RoundUpTo16(pixelWidth);
        var height = RoundUpTo16(pixelHeight);

        var view = _canvas.View;
        var expanded = FractalView.FromCenter(view.CenterA, view.CenterB,
            view.SpanA * height / pixelHeight, view.SpanB * width / pixelWidth);

        return (new RenderJob(expanded, _effectivePattern, InitialValue, Warmup, Iterations,
            SelectedDevice.Index, width, height), width, height);
    }

    private static int RoundUpTo16(double value) =>
        Math.Max(16, ((int)Math.Ceiling(value) + 15) / 16 * 16);

    /// <summary>
    /// The OpenCL kernels sample A and B through <c>float</c> tables. Near a = 4 the float spacing is
    /// about 2.4e-7, so once a pixel step falls below that the GPU renders visible stair-steps while the
    /// double-precision CPU paths keep resolving detail.
    /// </summary>
    private void UpdatePrecisionWarning(FractalView view, int renderWidth)
    {
        var isGpu = SelectedDevice?.Kind is DeviceKind.OpenCl or DeviceKind.OpenClPerf;
        var stepPerPixel = view.SpanB / Math.Max(renderWidth, 1);

        PrecisionWarning = isGpu && stepPerPixel < 5e-7
            ? "float precision limit reached — switch to a CPU device to zoom deeper"
            : null;
    }

    private void OnRenderCompleted(RenderOutput output) => Dispatcher.UIThread.Post(() =>
    {
        if (_canvas is null) return;

        ErrorText = null;
        _canvas.ShowResult(output, SelectedPalette, Contrast);

        var pixels = (double)output.Width * output.Height;
        var seconds = Math.Max(output.Elapsed.TotalSeconds, 1e-6);
        var mips = pixels * Math.Max(Iterations - Warmup, 1) / 1e6 / seconds;

        StatusText = string.Create(CultureInfo.InvariantCulture,
            $"{output.DeviceName}  ·  {output.Width}×{output.Height}{(output.IsPreview ? " (preview)" : "")}  ·  {output.Elapsed.TotalMilliseconds:0} ms  ·  {mips:0} Mit/s");
    });

    private void Recolor() => _canvas?.Recolor(SelectedPalette, Contrast);

    private void OnCursorMoved(CursorReadout readout)
    {
        var value = readout.HasExponent
            ? float.IsNaN(readout.Exponent) ? "NaN"
              : float.IsInfinity(readout.Exponent) ? "±inf"
              : readout.Exponent.ToString("0.0000", CultureInfo.InvariantCulture)
            : "—";

        CursorText = string.Create(CultureInfo.InvariantCulture,
            $"a {readout.A:0.000000}   b {readout.B:0.000000}   λ {value}");
    }

    #endregion

    #region Export

    private async Task SaveImageAsync()
    {
        if (_canvas is null || _window is null) return;

        var (job, width, height) = BuildJob();
        if (job is null) return;

        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save fractal image",
            SuggestedFileName = $"lyapunov-{_effectivePattern}-{Iterations}.png",
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("PNG image")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"],
                    AppleUniformTypeIdentifiers = ["public.png"],
                }
            ],
        });

        if (file is null) return;

        try
        {
            StatusText = "Rendering image for export…";
            IsBusy = true;

            var exportWidth = RoundUpTo16(width * (double)ExportScale);
            var exportHeight = RoundUpTo16(height * (double)ExportScale);
            var output = await _service.RenderOnceAsync(job, exportWidth, exportHeight);

            using var bitmap = new WriteableBitmap(new PixelSize(output.Width, output.Height),
                new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            unsafe
            {
                using var frame = bitmap.Lock();
                SelectedPalette.Colorize(output.Map, (byte*)frame.Address, frame.RowBytes, Contrast);
            }

            await using var stream = await file.OpenWriteAsync();
            bitmap.Save(stream, new PngBitmapEncoderOptions());

            StatusText = $"Saved {output.Width}×{output.Height} to {file.Name}";
        }
        catch (Exception ex)
        {
            ErrorText = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    public ValueTask DisposeAsync() => _service.DisposeAsync();
}

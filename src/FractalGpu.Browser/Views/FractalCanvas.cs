using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FractalGpu.Browser.Core;

namespace FractalGpu.Browser.Views;

public readonly record struct CursorReadout(double A, double B, float Exponent, bool HasExponent);

/// <summary>
/// The interactive fractal viewport.
/// <para>
/// The control keeps the last rendered exponent map together with the exact <see cref="FractalView"/>
/// it was rendered for. Drawing maps that region into the <em>current</em> view, so panning and zooming
/// resample the existing bitmap at once and stay responsive while a fresh render is still running —
/// the same trick map applications use. When the new map arrives it simply replaces the old one.
/// </para>
/// </summary>
public sealed class FractalCanvas : Control
{
    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromRgb(12, 12, 16));
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 214, 102)), 1.5);
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.FromArgb(40, 255, 214, 102));

    private WriteableBitmap? _bitmap;
    private RenderOutput? _output;
    private Palette _palette = Palette.Classic;
    private double _contrast = 1.7;

    private Point? _panOrigin;
    private FractalView _panStartView;
    private Point? _selectionStart;
    private Point? _selectionCurrent;

    public FractalCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public FractalView View { get; private set; } = Presets.Default.View;

    /// <summary>True while the shown bitmap is a coarse pass, so the status bar can say so.</summary>
    public bool ShowingPreview => _output?.IsPreview ?? false;

    /// <summary>Raised whenever the user moves the view. The flag marks the end of a gesture.</summary>
    public event Action<FractalView, bool>? ViewChanged;

    public event Action<CursorReadout>? CursorMoved;
    public event Action? CursorLeft;

    public bool SmoothScaling
    {
        set => RenderOptions.SetBitmapInterpolationMode(this,
            value ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None);
    }

    /// <summary>Moves the view without attributing the change to a user gesture (presets, history, reset).</summary>
    public void SetView(FractalView view)
    {
        View = view.FitAspect(Bounds.Width, Bounds.Height);
        InvalidateVisual();
    }

    public void ShowResult(RenderOutput output, Palette palette, double contrast)
    {
        _output = output;
        _palette = palette;
        _contrast = contrast;

        EnsureBitmap(output.Width, output.Height);
        Colorize();
        InvalidateVisual();
    }

    /// <summary>
    /// Re-maps the cached exponents through a different palette or contrast. This is the whole point of
    /// keeping the float map around: changing the look costs a colour pass, not a fractal recomputation.
    /// </summary>
    public void Recolor(Palette palette, double contrast)
    {
        _palette = palette;
        _contrast = contrast;
        if (_output is null) return;

        Colorize();
        InvalidateVisual();
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is not null && _bitmap.PixelSize.Width == width && _bitmap.PixelSize.Height == height)
            return;

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    private unsafe void Colorize()
    {
        if (_bitmap is null || _output is null) return;

        using var frame = _bitmap.Lock();
        _palette.Colorize(_output.Map, (byte*)frame.Address, frame.RowBytes, _contrast);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Backdrop, new Rect(Bounds.Size));

        if (_bitmap is not null && _output is not null)
        {
            var source = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
            context.DrawImage(_bitmap, source, DestinationRect(_output.View));
        }

        if (_selectionStart is { } start && _selectionCurrent is { } current)
        {
            var rect = new Rect(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y),
                Math.Abs(current.X - start.X), Math.Abs(current.Y - start.Y));
            context.FillRectangle(SelectionFill, rect);
            context.DrawRectangle(null, SelectionPen, rect);
        }
    }

    /// <summary>Where a bitmap covering <paramref name="bitmapView"/> lands under the current view.</summary>
    private Rect DestinationRect(FractalView bitmapView)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var spanB = Math.Max(View.SpanB, FractalView.MinSpan);
        var spanA = Math.Max(View.SpanA, FractalView.MinSpan);

        var x = (bitmapView.MinB - View.MinB) / spanB * w;
        var width = bitmapView.SpanB / spanB * w;
        var y = (View.MaxA - bitmapView.MaxA) / spanA * h;
        var height = bitmapView.SpanA / spanA * h;

        return new Rect(x, y, Math.Max(width, 0), Math.Max(height, 0));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty) return;

        // Keep one screen pixel worth the same parameter distance on both axes.
        View = View.FitAspect(Bounds.Width, Bounds.Height);
        ViewChanged?.Invoke(View, true);
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);
        var properties = point.Properties;
        var selecting = properties.IsRightButtonPressed ||
                        (properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

        if (selecting)
        {
            _selectionStart = point.Position;
            _selectionCurrent = point.Position;
        }
        else if (properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                Apply(View.ZoomAt(2.0, point.Position.X, point.Position.Y, Bounds.Width, Bounds.Height), true);
                return;
            }

            _panOrigin = point.Position;
            _panStartView = View;
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }

        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);

        if (_selectionStart is not null)
        {
            _selectionCurrent = position;
            InvalidateVisual();
        }
        else if (_panOrigin is { } origin)
        {
            var view = _panStartView.PanByPixels(position.X - origin.X, position.Y - origin.Y,
                Bounds.Width, Bounds.Height);
            Apply(view, false);
        }

        ReportCursor(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        if (_selectionStart is { } start && _selectionCurrent is { } end)
        {
            _selectionStart = null;
            _selectionCurrent = null;

            // A stray click must not zoom to a degenerate rectangle.
            if (Math.Abs(end.X - start.X) > 4 && Math.Abs(end.Y - start.Y) > 4)
            {
                var view = View.FromPixelRect(start.X, start.Y, end.X, end.Y, Bounds.Width, Bounds.Height);
                Apply(view.FitAspect(Bounds.Width, Bounds.Height), true);
            }
            else
            {
                InvalidateVisual();
            }
        }
        else if (_panOrigin is not null)
        {
            _panOrigin = null;
            Cursor = new Cursor(StandardCursorType.Cross);
            ViewChanged?.Invoke(View, true);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var position = e.GetPosition(this);
        var factor = Math.Pow(1.25, e.Delta.Y);
        if (Math.Abs(e.Delta.Y) < 1e-6) return;

        Apply(View.ZoomAt(factor, position.X, position.Y, Bounds.Width, Bounds.Height), true);
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        CursorLeft?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        const double step = 0.15;
        var handled = true;

        switch (e.Key)
        {
            case Key.Left: Apply(View.PanByPixels(Bounds.Width * step, 0, Bounds.Width, Bounds.Height), true); break;
            case Key.Right: Apply(View.PanByPixels(-Bounds.Width * step, 0, Bounds.Width, Bounds.Height), true); break;
            case Key.Up: Apply(View.PanByPixels(0, Bounds.Height * step, Bounds.Width, Bounds.Height), true); break;
            case Key.Down: Apply(View.PanByPixels(0, -Bounds.Height * step, Bounds.Width, Bounds.Height), true); break;
            case Key.Add or Key.OemPlus:
                Apply(View.ZoomAt(2, Bounds.Width / 2, Bounds.Height / 2, Bounds.Width, Bounds.Height), true);
                break;
            case Key.Subtract or Key.OemMinus:
                Apply(View.ZoomAt(0.5, Bounds.Width / 2, Bounds.Height / 2, Bounds.Width, Bounds.Height), true);
                break;
            default: handled = false; break;
        }

        e.Handled = handled;
    }

    private void Apply(FractalView view, bool gestureFinished)
    {
        View = view;
        InvalidateVisual();
        ViewChanged?.Invoke(view, gestureFinished);
    }

    private void ReportCursor(Point position)
    {
        var (a, b) = View.ToParams(position.X, position.Y, Bounds.Width, Bounds.Height);

        if (_output is null)
        {
            CursorMoved?.Invoke(new CursorReadout(a, b, 0, false));
            return;
        }

        // Look the exponent up in the map that is actually on screen, which may cover a different
        // region than the current view if a render is still pending.
        var source = _output.View;
        var i = (int)((b - source.MinB) / Math.Max(source.SpanB, FractalView.MinSpan) * _output.Width);
        var j = (int)((a - source.MinA) / Math.Max(source.SpanA, FractalView.MinSpan) * _output.Height);

        if (i < 0 || j < 0 || i >= _output.Width || j >= _output.Height)
        {
            CursorMoved?.Invoke(new CursorReadout(a, b, 0, false));
            return;
        }

        CursorMoved?.Invoke(new CursorReadout(a, b, _output.Map[i, j], true));
    }

    #endregion
}

using Avalonia.Controls;
using FractalGpu.Browser.ViewModels;

namespace FractalGpu.Browser.Views;

public partial class MainWindow : Window
{
    private bool _attached;

    public MainWindow()
    {
        InitializeComponent();

        // The canvas owns the current view and raises gestures; the view model drives rendering.
        // They are wired directly rather than through bindings, because an exponent map and a pixel
        // buffer are not view-model shaped data.
        DataContextChanged += (_, _) =>
        {
            if (_attached || DataContext is not MainWindowViewModel viewModel) return;
            _attached = true;

            Width = viewModel.InitialWindowWidth;
            Height = viewModel.InitialWindowHeight;
            viewModel.Attach(Canvas, this);
        };
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FractalGpu.Browser.ViewModels;
using FractalGpu.Browser.Views;

namespace FractalGpu.Browser;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };

            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => viewModel.PersistState(window);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

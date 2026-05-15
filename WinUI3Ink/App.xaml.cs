using Microsoft.UI.Xaml;

namespace WinUI3Ink;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window != null)
            return;

        _window = new MainWindow();
        _window.Activate();
    }
}

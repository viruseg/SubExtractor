using Microsoft.UI.Xaml;
using SubExtractor.Services;

namespace SubExtractor;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new AppServices();
        _window = new MainWindow(services);
        _window.Activate();
    }
}

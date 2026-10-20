using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SubExtractor.Services;
using Windows.Graphics;

namespace SubExtractor;

public sealed partial class MainWindow : Window
{
    private readonly AppServices _services;
    private bool _startupStarted;

    public MainWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(1100, 720));

        _services.Busy.Changed += Services_BusyChanged;
        MainRoot.Loaded += MainWindow_Loaded;
        AppWindow.Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        ApplyBusyState();
    }

    private void Services_BusyChanged(object? sender, EventArgs e) => ApplyBusyState();

    private void ApplyBusyState()
    {
        var isEnabled = !_services.Busy.IsBusy;
        ContentHost.IsEnabled = isEnabled;
        SettingsButton.IsEnabled = isEnabled;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupStarted)
        {
            return;
        }

        _startupStarted = true;

        if (!await _services.Bootstrapper.EnsureInstalledAsync(this))
        {
            Application.Current.Exit();
        }
    }

    private void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_services.Busy.IsUpdate)
        {
            args.Cancel = true;
            return;
        }

        if (_services.Operations.IsRunning)
        {
            args.Cancel = true;
            _services.Operations.RequestExit();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args) => Application.Current.Exit();
}

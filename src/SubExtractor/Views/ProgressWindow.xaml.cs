using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using SubExtractor.Services;
using SubExtractor.ViewModels;
using Windows.Graphics;

namespace SubExtractor.Views;

public sealed partial class ProgressWindow : Window
{
    private readonly ActivityLog _log;

    public ProgressWindow(ActivityLog log, Window owner)
    {
        _log = log;
        InitializeComponent();
        WindowOwner.SetOwner(this, owner);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(760, 520));
        WindowOwner.CenterOnOwner(this, owner);

        LogList.ItemsSource = log.Entries;
        log.PropertyChanged += Log_PropertyChanged;
        log.Entries.CollectionChanged += Entries_CollectionChanged;
        RootGrid.Loaded += RootGrid_Loaded;
        Closed += OnClosed;

        ApplyState();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e) => ScrollToLast();

    private void Log_PropertyChanged(object? sender, PropertyChangedEventArgs e) => ApplyState();

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScrollToLast();

    private void ApplyState()
    {
        Title = _log.Title;
        ProgressPanel.Visibility = _log.IsRunning ? Visibility.Visible : Visibility.Collapsed;

        if (_log.Progress is { } percent)
        {
            var value = Math.Clamp(percent, 0, 100);
            OverallProgress.IsIndeterminate = false;
            OverallProgress.Value = value;
            PercentageText.Text = $"{value:0}%";
        }
        else
        {
            OverallProgress.IsIndeterminate = true;
            PercentageText.Text = string.Empty;
        }
    }

    private void ScrollToLast()
    {
        if (_log.Entries.Count > 0)
        {
            LogList.ScrollIntoView(_log.Entries[^1]);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _log.PropertyChanged -= Log_PropertyChanged;
        _log.Entries.CollectionChanged -= Entries_CollectionChanged;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class LogForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => Application.Current.Resources[
            value is true ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush"];

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

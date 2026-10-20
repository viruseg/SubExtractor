using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<LogEntry> _entries = [];
    private readonly TaskCompletionSource _closed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _loaded =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _finished;
    private bool _cancelRaised;

    public ProgressWindow(string title, Window owner)
    {
        InitializeComponent();
        Title = title;
        WindowOwner.SetOwner(this, owner);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(760, 520));
        LogList.ItemsSource = _entries;
        RootGrid.Loaded += (_, _) => _loaded.TrySetResult();
        AppWindow.Closing += OnClosing;
        Closed += OnClosed;
    }

    public event EventHandler? CancelRequested;

    public void SetOverall(double? percent)
    {
        if (percent is null)
        {
            OverallProgress.IsIndeterminate = true;
            PercentageText.Text = "";
            return;
        }

        var value = Math.Clamp(percent.Value, 0, 100);
        OverallProgress.IsIndeterminate = false;
        OverallProgress.Value = value;
        PercentageText.Text = $"{value:0}%";
    }

    public LogEntry AddLog(string message, bool isError = false)
    {
        var entry = new LogEntry(DateTime.Now.ToString("HH:mm:ss"), message, isError);
        _entries.Add(entry);
        LogList.ScrollIntoView(entry);
        return entry;
    }

    public void UpdateLog(LogEntry entry, string message, bool isError = false)
    {
        entry.Message = message;
        entry.IsError = isError;
        LogList.ScrollIntoView(entry);
    }

    public void Finish(string summary)
    {
        AddLog(summary);
        CancelButton.Content = "Close";
        CancelButton.IsEnabled = true;
        _finished = true;
    }

    public void DisableCancel()
    {
        if (!_finished)
        {
            CancelButton.IsEnabled = false;
        }
    }

    public void AllowClose() => _finished = true;

    public Task WaitForCloseAsync() => _closed.Task;

    public Task WaitForLoadedAsync() => _loaded.Task;

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_finished)
        {
            args.Cancel = true;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args) => _closed.TrySetResult();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_finished)
        {
            Close();
            return;
        }

        if (_cancelRaised)
        {
            return;
        }

        _cancelRaised = true;
        CancelButton.IsEnabled = false;
        CancelButton.Content = "Cancelling...";
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class LogForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => Application.Current.Resources[
            value is true ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush"];

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

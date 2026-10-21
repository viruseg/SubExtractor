using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SubExtractor.Core.Extraction;
using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;
using SubExtractor.Services;
using SubExtractor.ViewModels;
using SubExtractor.Views;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace SubExtractor;

public sealed partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly SolidColorBrush _folderBorderBrush = new(Colors.Red);
    private SettingsWindow? _settingsWindow;
    private bool _startupStarted;
    private bool _suppressFolderCommit;
    private bool _isCommittingFolder;
    private bool _isExtracting;
    private string? _currentFolder;

    public MainWindow(AppServices services)
    {
        _services = services;
        ViewModel = new MainViewModel();
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(1100, 720));

        _folderBorderBrush.Opacity = 0;
        FolderBorder.BorderBrush = _folderBorderBrush;

        _services.Busy.Changed += Services_BusyChanged;
        MainRoot.Loaded += MainWindow_Loaded;
        AppWindow.Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        ApplyBusyState();
    }

    public MainViewModel ViewModel { get; }

    private void Services_BusyChanged(object? sender, EventArgs e) => ApplyBusyState();

    private void ApplyBusyState()
    {
        var isEnabled = !_services.Busy.IsBusy;
        ContentHost.IsEnabled = isEnabled;
        ContentHost.IsHitTestVisible = isEnabled;
        SettingsButton.IsEnabled = isEnabled;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_services, this);
        settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = settingsWindow;
        settingsWindow.Activate();
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

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressFolderCommit = true;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            FolderBox.Text = folder.Path;
            await StartScanAsync(folder.Path);
        }
        finally
        {
            _suppressFolderCommit = false;
        }
    }

    private void FolderBox_Paste(object sender, TextControlPasteEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = CommitFolderInputAsync());

    private void FolderBox_LostFocus(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            var xamlRoot = Content.XamlRoot;
            if (xamlRoot is not null &&
                ReferenceEquals(FocusManager.GetFocusedElement(xamlRoot), BrowseButton))
            {
                return;
            }

            _ = CommitFolderInputAsync();
        });

    private void FolderBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        _ = CommitFolderInputAsync();
    }

    private async Task CommitFolderInputAsync()
    {
        if (_suppressFolderCommit || _isCommittingFolder)
        {
            return;
        }

        _isCommittingFolder = true;
        try
        {
            var path = PathInput.Sanitize(FolderBox.Text);

            if (path.Length == 0)
            {
                ClearResults("Choose a folder to scan.");
                return;
            }

            if (!Directory.Exists(path))
            {
                FolderBox.Text = string.Empty;
                ClearResults("Choose a folder to scan.");
                _ = FlashRedAsync();
                FolderBox.Focus(FocusState.Programmatic);
                return;
            }

            FolderBox.Text = path;
            await StartScanAsync(path);
        }
        finally
        {
            _isCommittingFolder = false;
        }
    }

    private async Task StartScanAsync(string folder)
    {
        if (_isExtracting || _services.Busy.IsBusy || _services.Operations.IsRunning)
        {
            return;
        }

        ClearResults(string.Empty);
        _currentFolder = folder;

        using var busy = _services.Busy.Begin();

        var progress = new ProgressWindow("Scanning", this);
        progress.Activate();
        progress.AddLog("Scan started");
        progress.SetOverall(0);

        var sink = new ScanProgressSink(progress, DispatcherQueue);

        ScanResult result;
        try
        {
            using (var operation = _services.Operations.Begin())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
            {
                EventHandler cancelHandler = (_, _) => cts.Cancel();
                progress.CancelRequested += cancelHandler;
                try
                {
                    result = await _services.Scanner.ScanAsync(folder, sink, cts.Token);
                }
                finally
                {
                    // WHY: the CTS is disposed at scope exit; a late Cancel must not reach it.
                    progress.CancelRequested -= cancelHandler;
                    progress.DisableCancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            progress.AllowClose();
            progress.Close();
            ViewModel.SetVideos([]);
            ViewModel.EmptyStateText = "Scan cancelled.";
            ViewModel.StatusText = "Scan cancelled.";
            return;
        }

        if (result.FolderError is not null)
        {
            ClearResults("Couldn't read this folder.");
            await sink.FlushAsync();
            progress.Finish("Scan finished: 0 files, 0 subtitle tracks");
            await progress.WaitForCloseAsync();
            return;
        }

        ViewModel.SetVideos(result.Videos);
        var trackCount = result.Videos.Sum(video => video.Tracks.Count);

        if (result.Videos.Count == 0)
        {
            ViewModel.EmptyStateText = "No video files found in this folder.";
        }

        ViewModel.StatusText = $"{result.Videos.Count} videos, {trackCount} subtitle tracks";
        await sink.FlushAsync();
        progress.Finish($"Scan finished: {result.Videos.Count} files, {trackCount} subtitle tracks");
        await progress.WaitForCloseAsync();
    }

    private async void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isExtracting || _services.Busy.IsBusy || _services.Operations.IsRunning)
        {
            return;
        }

        var tracks = ViewModel.SelectedTracks;
        if (tracks.Count == 0 || _currentFolder is not { Length: > 0 } folder)
        {
            return;
        }

        _isExtracting = true;
        ProgressWindow? progress = null;
        try
        {
            using var busy = _services.Busy.Begin();

            var plans = OutputPlanner.Plan(folder, tracks, File.Exists);
            if (plans.Any(plan => plan.OverwritesExisting) &&
                !await ConfirmOverwriteAsync(plans))
            {
                return;
            }

            progress = new ProgressWindow("Extracting", this);
            progress.Activate();
            await progress.WaitForLoadedAsync();
            progress.AddLog("Extraction started");
            progress.SetOverall(0);

            var sink = new ExtractionProgressSink(progress, DispatcherQueue);

            ExtractionSummary summary;
            try
            {
                using (var operation = _services.Operations.Begin())
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
                {
                    EventHandler cancelHandler = (_, _) => cts.Cancel();
                    progress.CancelRequested += cancelHandler;
                    try
                    {
                        summary = await _services.Extractor.ExtractAsync(plans, sink, cts.Token);
                    }
                    finally
                    {
                        // WHY: the CTS is disposed at scope exit; a late Cancel must not reach it.
                        progress.CancelRequested -= cancelHandler;
                        progress.DisableCancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                progress.AllowClose();
                progress.Close();
                ViewModel.StatusText = "Extraction cancelled.";
                return;
            }
            catch (Exception ex)
            {
                await sink.FlushAsync();
                progress.AddLog($"Extraction failed: {ex.Message}", isError: true);
                progress.Finish("Extraction failed.");
                ViewModel.StatusText = "Extraction failed.";
                await progress.WaitForCloseAsync();
                return;
            }

            await sink.FlushAsync();
            var result = $"Extraction finished: {summary.Succeeded} succeeded, {summary.Failed} failed.";
            ViewModel.StatusText = result;
            progress.Finish(result);
            await progress.WaitForCloseAsync();
        }
        catch (Exception ex)
        {
            if (progress is not null)
            {
                progress.AllowClose();
                progress.Close();
            }

            ViewModel.StatusText = $"Extraction failed: {ex.Message}";
        }
        finally
        {
            _isExtracting = false;
        }
    }

    private async Task<bool> ConfirmOverwriteAsync(IReadOnlyList<OutputPlan> plans)
    {
        if (ContentRoot.XamlRoot is not { } xamlRoot)
        {
            return false;
        }

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = "The following subtitle files already exist and will be overwritten:",
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var line in OverwritePrompt.BuildLines(plans))
        {
            content.Children.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Overwrite subtitles?",
            Content = content,
            PrimaryButtonText = "Overwrite",
            CloseButtonText = "Cancel",
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ClearResults(string emptyStateText)
    {
        ViewModel.SetVideos([]);
        ViewModel.EmptyStateText = emptyStateText;
        ViewModel.StatusText = string.Empty;
    }

    private void ColumnHeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: TableColumnViewModel column })
        {
            column.ToggleAll();
        }
    }

    private void BodyScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (HeaderScrollViewer.HorizontalOffset != BodyScrollViewer.HorizontalOffset)
        {
            HeaderScrollViewer.ChangeView(
                BodyScrollViewer.HorizontalOffset, null, null, disableAnimation: true);
        }
    }

    private async Task FlashRedAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            _folderBorderBrush.Opacity = 1;
            await Task.Delay(100);
            _folderBorderBrush.Opacity = 0;
            await Task.Delay(100);
        }
    }
}

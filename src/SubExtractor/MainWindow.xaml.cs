using System.ComponentModel;
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
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace SubExtractor;

public sealed partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly SolidColorBrush _folderBorderBrush = new(Colors.Red);
    private readonly Brush _folderDropBrush;
    private SettingsWindow? _settingsWindow;
    private ProgressWindow? _logWindow;
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
        _folderDropBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        FolderBorder.BorderBrush = _folderBorderBrush;

        _services.Busy.Changed += Services_BusyChanged;
        MainRoot.Loaded += MainWindow_Loaded;
        AppWindow.Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Log.PropertyChanged += Log_PropertyChanged;
        ApplyStatusForeground();
        ApplyBusyState();
    }

    public MainViewModel ViewModel { get; }

    public ActivityLog Log => _services.Log;

    private void Services_BusyChanged(object? sender, EventArgs e) => ApplyBusyState();

    private void Log_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActivityLog.LastIsError))
        {
            ApplyStatusForeground();
        }
    }

    private void ApplyStatusForeground() =>
        StatusTextBlock.Foreground = (Brush)Application.Current.Resources[
            Log.LastIsError ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];

    private void ApplyBusyState()
    {
        var isEnabled = !_services.Busy.IsBusy;
        ContentHost.IsEnabled = isEnabled;
        ContentHost.IsHitTestVisible = isEnabled;
        SettingsButton.IsEnabled = isEnabled;
        ExtractAllButton.IsEnabled = isEnabled && ViewModel.CanExtract;
    }

    private void StatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (_logWindow is not null)
        {
            _logWindow.Activate();
            return;
        }

        var logWindow = new ProgressWindow(Log, this);
        logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow = logWindow;
        logWindow.Activate();
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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => _ = StartUpAsync();

    private async Task StartUpAsync()
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

    private void BrowseButton_Click(object sender, RoutedEventArgs e) => _ = BrowseAsync();

    private async Task BrowseAsync()
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

    private void FolderBorder_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Scan this folder";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
        e.Handled = true;
        FolderBorder.BorderBrush = _folderDropBrush;
    }

    private void FolderBorder_DragLeave(object sender, DragEventArgs e) =>
        FolderBorder.BorderBrush = _folderBorderBrush;

    private void FolderBorder_Drop(object sender, DragEventArgs e) => _ = AcceptDropAsync(e);

    private async Task AcceptDropAsync(DragEventArgs e)
    {
        e.Handled = true;
        FolderBorder.BorderBrush = _folderBorderBrush;

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        string? folder;
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            folder = await ResolveDroppedFolderAsync(items);
        }
        catch (Exception)
        {
            // NOTE: the shell can revoke a data package while it is being read.
            return;
        }

        if (folder is null)
        {
            return;
        }

        FolderBox.Text = folder;
        await CommitFolderInputAsync();
    }

    private static async Task<string?> ResolveDroppedFolderAsync(IReadOnlyList<IStorageItem> items)
    {
        foreach (var item in items)
        {
            if (item is StorageFolder { Path.Length: > 0 } folder)
            {
                return folder.Path;
            }

            if (item is StorageFile { Path.Length: > 0 } file)
            {
                var parent = await file.GetParentAsync();
                if (parent is { Path.Length: > 0 })
                {
                    return parent.Path;
                }
            }
        }

        return null;
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

        var log = _services.Log;
        log.BeginOperation("Scanning");
        log.Add($"Scan started: {Path.GetFullPath(folder)}");
        log.SetProgress(0);

        try
        {
            var sink = new ScanProgressSink(log, DispatcherQueue);

            ScanResult result;
            try
            {
                using (var operation = _services.Operations.Begin())
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
                {
                    EventHandler cancelHandler = (_, _) => cts.Cancel();
                    log.CancelRequested += cancelHandler;
                    try
                    {
                        result = await _services.Scanner.ScanAsync(folder, sink, cts.Token);
                    }
                    finally
                    {
                        // WHY: the CTS is disposed at scope exit; a late Cancel must not reach it.
                        log.CancelRequested -= cancelHandler;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await sink.FlushAsync();
                log.Add("Scan cancelled.");
                ViewModel.SetVideos([]);
                ViewModel.EmptyStateText = "Scan cancelled.";
                return;
            }

            if (result.FolderError is not null)
            {
                ClearResults("Couldn't read this folder.");
                await sink.FlushAsync();
                log.Add("Scan finished: 0 files, 0 subtitle tracks");
                return;
            }

            ViewModel.SetVideos(result.Videos);
            var trackCount = result.Videos.Sum(video => video.Tracks.Count);

            if (result.Videos.Count == 0)
            {
                ViewModel.EmptyStateText = "No video files found in this folder.";
            }

            await sink.FlushAsync();
            log.Add($"Scan finished: {result.Videos.Count} files, {trackCount} subtitle tracks");
        }
        finally
        {
            log.EndOperation();
        }
    }

    private void ExtractAll_Click(object sender, RoutedEventArgs e) => _ = ExtractAllAsync();

    private async Task ExtractAllAsync()
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
        var log = _services.Log;
        var started = false;
        try
        {
            using var busy = _services.Busy.Begin();

            var plans = OutputPlanner.Plan(folder, tracks, File.Exists);
            if (plans.Any(plan => plan.OverwritesExisting) &&
                !await ConfirmOverwriteAsync(plans))
            {
                return;
            }

            log.BeginOperation("Extracting");
            started = true;
            log.Add("Extraction started");
            log.SetProgress(0);

            var sink = new ExtractionProgressSink(log, DispatcherQueue);

            ExtractionSummary summary;
            try
            {
                using (var operation = _services.Operations.Begin())
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
                {
                    EventHandler cancelHandler = (_, _) => cts.Cancel();
                    log.CancelRequested += cancelHandler;
                    try
                    {
                        var overwrite = await _services.OverwriteRecycler.RecycleAsync(plans, cts.Token);
                        ReportRecycleFailures(log, overwrite.Failures);

                        summary = await _services.Extractor.ExtractAsync(
                            overwrite.ExtractablePlans, sink, cts.Token);
                    }
                    finally
                    {
                        // WHY: the CTS is disposed at scope exit; a late Cancel must not reach it.
                        log.CancelRequested -= cancelHandler;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await sink.FlushAsync();
                log.Add("Extraction cancelled.");
                return;
            }
            catch (Exception ex)
            {
                await sink.FlushAsync();
                log.Add($"Extraction failed: {ex.Message}", isError: true);
                return;
            }

            await sink.FlushAsync();
            log.Add($"Extraction finished: {summary.Succeeded} succeeded, {summary.Failed} failed.");
        }
        catch (Exception ex)
        {
            log.Add($"Extraction failed: {ex.Message}", isError: true);
        }
        finally
        {
            if (started)
            {
                log.EndOperation();
            }

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
            Text = "The following subtitle files already exist and will be moved to the Recycle Bin:",
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
            Title = "Replace subtitles?",
            Content = content,
            PrimaryButtonText = "Replace",
            CloseButtonText = "Cancel",
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static void ReportRecycleFailures(ActivityLog log, IReadOnlyList<FileRecycleFailure> failures)
    {
        foreach (var failure in failures)
        {
            log.Add(
                $"Could not move '{Path.GetFileName(failure.Path)}' to the Recycle Bin: " +
                $"{failure.Stage} (0x{failure.HResult:X8}) after {failure.Attempts} attempt(s). " +
                "This subtitle will be skipped.",
                isError: true);
        }
    }

    private void ClearResults(string emptyStateText)
    {
        ViewModel.SetVideos([]);
        ViewModel.EmptyStateText = emptyStateText;
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

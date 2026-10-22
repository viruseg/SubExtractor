using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SubExtractor.Core.Extraction;
using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;
using SubExtractor.Core.Scanning;
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
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _folderStateTimer;
    private SettingsWindow? _settingsWindow;
    private ProgressWindow? _logWindow;
    private bool _startupStarted;
    private bool _suppressFolderCommit;
    private bool _isCommittingFolder;
    private bool _isExtracting;
    private int _folderStateVersion;
    private ExtractionPhase _extractionPhase;
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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        MainRoot.Loaded += MainWindow_Loaded;
        AppWindow.Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Log.PropertyChanged += Log_PropertyChanged;

        _folderStateTimer = DispatcherQueue.CreateTimer();
        _folderStateTimer.Interval = TimeSpan.FromMilliseconds(300);
        _folderStateTimer.IsRepeating = false;
        _folderStateTimer.Tick += (_, _) => RefreshOpenFolderState();

        ApplyStatusForeground();
        ApplyBusyState();
    }

    public MainViewModel ViewModel { get; }

    public ActivityLog Log => _services.Log;

    private void Services_BusyChanged(object? sender, EventArgs e) => ApplyBusyState();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CanExtract))
        {
            ApplyBusyState();
        }
    }

    private void Log_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActivityLog.LastIsError))
        {
            ApplyStatusForeground();
            return;
        }

        if (_extractionPhase == ExtractionPhase.Idle)
        {
            return;
        }

        if (e.PropertyName == nameof(ActivityLog.IsCancelRequested) && Log.IsCancelRequested)
        {
            SetExtractionPhase(ExtractionPhase.Stopping);
        }
        else if (e.PropertyName == nameof(ActivityLog.Progress))
        {
            ApplyExtractionButtonVisuals();
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
        ExtractAllButton.IsEnabled = _isExtracting || (isEnabled && ViewModel.CanExtract);
        RefreshOpenFolderState();

        if (_extractionPhase == ExtractionPhase.Idle)
        {
            ApplyExtractionButtonVisuals();
        }
    }

    private void RefreshOpenFolderState()
    {
        var version = ++_folderStateVersion;
        var text = FolderBox.Text;

        if (PathInput.Sanitize(text).Length == 0)
        {
            OpenFolderButton.IsEnabled = false;
            return;
        }

        _ = RefreshOpenFolderStateAsync(version, text);
    }

    private async Task RefreshOpenFolderStateAsync(int version, string text)
    {
        var exists = await Task.Run(() => PathInput.IsExistingFolder(text));

        if (version == _folderStateVersion)
        {
            OpenFolderButton.IsEnabled = exists;
        }
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

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) => _ = OpenFolderAsync();

    private async Task OpenFolderAsync()
    {
        var text = FolderBox.Text;
        var path = PathInput.Sanitize(text);

        if (path.Length == 0 || !await Task.Run(() => PathInput.IsExistingFolder(path)))
        {
            RefreshOpenFolderState();
            return;
        }

        if (!string.Equals(text, FolderBox.Text, StringComparison.Ordinal))
        {
            RefreshOpenFolderState();
            return;
        }

        var result = _services.FolderOpener.Open(path);
        if (result.ErrorMessage is { } error)
        {
            _services.Log.Add(error, isError: true);
            RefreshOpenFolderState();
        }
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
            RefreshOpenFolderState();
            await StartScanAsync(folder.Path);
        }
        finally
        {
            _suppressFolderCommit = false;
        }
    }

    private void FolderBox_Paste(object sender, TextControlPasteEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = CommitFolderInputAsync());

    private void FolderBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _folderStateTimer.Stop();
        _folderStateTimer.Start();
    }

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
        RefreshOpenFolderState();
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
                SetCurrentFolder(null);
                ClearResults("Choose a folder to scan.");
                RefreshOpenFolderState();
                return;
            }

            if (!Directory.Exists(path))
            {
                FolderBox.Text = string.Empty;
                SetCurrentFolder(null);
                ClearResults("Choose a folder to scan.");
                RefreshOpenFolderState();
                _ = FlashRedAsync();
                FolderBox.Focus(FocusState.Programmatic);
                return;
            }

            FolderBox.Text = path;
            RefreshOpenFolderState();
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
        SetCurrentFolder(folder);

        using var busy = _services.Busy.Begin();

        var log = _services.Log;
        log.BeginOperation("Scanning");
        log.Add(ScanReportBuilder.StartedText(folder));
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

            var report = ScanReportBuilder.Build(result);

            if (report.FolderFailed)
            {
                ClearResults(report.EmptyStateText ?? string.Empty);
                await sink.FlushAsync();
                log.Add(report.SummaryText);
                return;
            }

            ViewModel.SetVideos(result.Videos);
            UpdateFileContentWidth();
            UpdateFileColumnWidth();

            if (report.EmptyStateText is not null)
            {
                ViewModel.EmptyStateText = report.EmptyStateText;
            }

            await sink.FlushAsync();
            log.Add(report.SummaryText);
        }
        finally
        {
            log.EndOperation();
        }
    }

    private void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (_extractionPhase == ExtractionPhase.Running)
        {
            RequestStop();
        }
        else if (_extractionPhase == ExtractionPhase.Idle)
        {
            _ = ExtractAllAsync();
        }
    }

    private void RequestStop()
    {
        if (_extractionPhase != ExtractionPhase.Running)
        {
            return;
        }

        SetExtractionPhase(ExtractionPhase.Stopping);
        Log.RequestCancel();
    }

    private void SetExtractionPhase(ExtractionPhase phase)
    {
        if (_extractionPhase == phase)
        {
            return;
        }

        _extractionPhase = phase;
        ApplyBusyState();
        ApplyExtractionButtonVisuals();
    }

    private void ApplyExtractionButtonVisuals()
    {
        if (_extractionPhase == ExtractionPhase.Idle)
        {
            ExtractAllButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            ExtractAllButton.ClearValue(Control.BackgroundProperty);
            ExtractProgressFill.Visibility = Visibility.Collapsed;
            ExtractButtonLabel.Text = "Extract All";
            ExtractButtonLabel.Foreground = (Brush)Application.Current.Resources[
                ExtractAllButton.IsEnabled
                    ? "TextOnAccentFillColorPrimaryBrush"
                    : "TextFillColorDisabledBrush"];
            AutomationProperties.SetName(ExtractAllButton, "Extract all subtitles");
            return;
        }

        ExtractAllButton.Style =
            (Style)Application.Current.Resources["ExtractionProgressButtonStyle"];
        ExtractButtonLabel.Foreground =
            (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        var stopping = _extractionPhase == ExtractionPhase.Stopping;
        var xamlRoot = Content.XamlRoot;
        var hasKeyboardFocus = xamlRoot is not null &&
                               ExtractAllButton.FocusState == FocusState.Keyboard &&
                               ReferenceEquals(
                                   FocusManager.GetFocusedElement(xamlRoot), ExtractAllButton);
        var stop = stopping || ExtractAllButton.IsPointerOver || hasKeyboardFocus;

        if (stop)
        {
            ExtractAllButton.Background =
                (Brush)Application.Current.Resources["ExtractionStopBrush"];
            ExtractProgressFill.Visibility = Visibility.Collapsed;
            ExtractButtonLabel.Text = stopping ? "Stopping…" : "Stop";
            AutomationProperties.SetName(
                ExtractAllButton, stopping ? "Stopping extraction" : "Stop extraction");
            return;
        }

        ExtractAllButton.Background =
            (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
        ExtractProgressFill.Visibility = Visibility.Visible;

        var percent = Math.Clamp(Log.Progress ?? 0, 0, 100);
        var rounded = (int)Math.Round(percent, MidpointRounding.AwayFromZero);
        ExtractButtonLabel.Text = $"{rounded}%";
        ExtractProgressFill.Width = ExtractButtonSurface.ActualWidth * percent / 100;
        AutomationProperties.SetName(ExtractAllButton, $"Stop extraction — {rounded}% complete");
    }

    private void ExtractAllButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        ApplyExtractionButtonVisuals();

    private void ExtractAllButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        ApplyExtractionButtonVisuals();

    private void ExtractAllButton_GotFocus(object sender, RoutedEventArgs e) =>
        ApplyExtractionButtonVisuals();

    private void ExtractAllButton_LostFocus(object sender, RoutedEventArgs e) =>
        ApplyExtractionButtonVisuals();

    private void ExtractButtonSurface_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyExtractionButtonVisuals();

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
        var timeShift = _services.TimeShift.Shift;
        var textRemovalRules = _services.TextRemoval.EffectiveRules;
        var started = false;
        try
        {
            using var busy = _services.Busy.Begin();

            var plans = OutputPlanner.Plan(folder, tracks, File.Exists);
            if (ExtractionReport.HasOverwrites(plans) &&
                !await ConfirmOverwriteAsync(plans))
            {
                return;
            }

            log.BeginOperation("Extracting");
            started = true;
            log.Add("Extraction started");
            log.SetProgress(0);
            SetExtractionPhase(ExtractionPhase.Running);

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
                        foreach (var message in ExtractionReport.RecycleFailureMessages(overwrite.Failures))
                        {
                            log.Add(message, isError: true);
                        }

                        if (timeShift != TimeSpan.Zero || textRemovalRules.Count > 0)
                        {
                            foreach (var message in ExtractionReport.SrtOnlySkipMessages(
                                         overwrite.ExtractablePlans,
                                         timeShift != TimeSpan.Zero,
                                         textRemovalRules.Count > 0))
                            {
                                log.Add(message);
                            }
                        }

                        summary = await _services.Extractor.ExtractAsync(
                            overwrite.ExtractablePlans, timeShift, textRemovalRules, sink, cts.Token);
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
            log.Add(ExtractionReport.SummaryText(summary));
        }
        catch (Exception ex)
        {
            log.Add($"Extraction failed: {ex.Message}", isError: true);
        }
        finally
        {
            if (started)
            {
                SetExtractionPhase(ExtractionPhase.Idle);
                log.EndOperation();
            }

            _isExtracting = false;
            ApplyBusyState();
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

    private void ClearResults(string emptyStateText)
    {
        ViewModel.SetVideos([]);
        ViewModel.EmptyStateText = emptyStateText;
    }

    private void SetCurrentFolder(string? folder)
    {
        _currentFolder = folder;
        _services.TimeShift.SelectFolder(folder);
        _services.TextRemoval.SelectFolder(folder);
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TableColumnViewModel column })
        {
            column.ToggleAll();
        }
    }

    private void FileCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TableCellViewModel cell })
        {
            cell.Row?.ToggleAll();
        }
    }

    private void BodyScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateFileColumnWidth();

    private void BodyScrollViewer_LayoutUpdated(object sender, object e) =>
        UpdateFileColumnWidth();

    private void UpdateFileColumnWidth() =>
        ViewModel.SetFileColumnWidth(BodyScrollViewer.ViewportWidth);

    private void UpdateFileContentWidth()
    {
        var contentWidth = 0d;
        var statusWidths = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var row in ViewModel.Rows)
        {
            var cell = row.FileCell;
            var cellWidth = MeasureText(FileNameMeasurer, cell.FileName);

            if (!string.IsNullOrEmpty(cell.StatusText))
            {
                if (!statusWidths.TryGetValue(cell.StatusText, out var statusWidth))
                {
                    statusWidth = MeasureText(FileStatusMeasurer, cell.StatusText);
                    statusWidths[cell.StatusText] = statusWidth;
                }

                cellWidth += statusWidth + ColumnWidthPolicy.FileCellGap;
            }

            contentWidth = Math.Max(
                contentWidth,
                Math.Ceiling(cellWidth) + ColumnWidthPolicy.HorizontalPadding + 1);
        }

        ViewModel.SetFileContentWidth(contentWidth);
    }

    private static double MeasureText(TextBlock measurer, string text)
    {
        measurer.Text = text;
        measurer.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return measurer.DesiredSize.Width;
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

    private enum ExtractionPhase
    {
        Idle,
        Running,
        Stopping,
    }
}

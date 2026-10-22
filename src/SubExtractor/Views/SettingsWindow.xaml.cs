using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SubExtractor.Core.Extraction;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Services;
using SubExtractor.ViewModels;
using Windows.Foundation;
using Windows.Graphics;

namespace SubExtractor.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly AppServices _services;
    private readonly Window _owner;
    private bool _checkStarted;
    private bool _isChecking;
    private bool _isUpdating;
    private bool _shiftReady;
    private bool _updatingShift;
    private bool _rulesReady;
    private bool _updatingRules;
    private string? _latestVersionDisplay;

    public SettingsWindow(AppServices services, Window owner)
    {
        _services = services;
        _owner = owner;
        ViewModel = new SettingsViewModel();
        InitializeComponent();
        LoadTimeShift();
        LoadTextRules();
        _shiftReady = true;
        _rulesReady = true;
        WindowOwner.SetOwner(this, owner);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(640, 620));
        WindowOwner.CenterOnOwner(this, owner);

        _services.Busy.Changed += Services_BusyChanged;
        _services.TimeShift.Changed += TimeShift_Changed;
        _services.TextRemoval.Changed += TextRemoval_Changed;
        SettingsRoot.Loaded += SettingsWindow_Loaded;
        AppWindow.Closing += SettingsWindow_Closing;
        Closed += SettingsWindow_Closed;
    }

    public SettingsViewModel ViewModel { get; }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ResizeToContent();

        if (_checkStarted)
        {
            return;
        }

        _checkStarted = true;
        _ = CheckVersionsAsync();
    }

    private void ResizeToContent()
    {
        var scale = SettingsRoot.XamlRoot?.RasterizationScale ?? 1;
        var chromeHeight = AppWindow.Size.Height - AppWindow.ClientSize.Height;
        var contentHeight = SettingsRoot.DesiredSize.Height + GetBottomGapCompensation();
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var clientHeight = Math.Min((int)Math.Ceiling(contentHeight * scale), workArea.Height - chromeHeight);

        AppWindow.Resize(new SizeInt32(AppWindow.Size.Width, clientHeight + chromeHeight));
        WindowOwner.CenterOnOwner(this, _owner);
    }

    private double GetBottomGapCompensation()
    {
        if (SettingsRoot.Children[0] is not FrameworkElement first)
        {
            return 0;
        }

        // WHY: The first row centers its label, so mirror that visible inset at the bottom.
        var firstTop = first.TransformToVisual(SettingsRoot).TransformPoint(new Point(0, 0)).Y;
        return Math.Max(0, firstTop - SettingsRoot.Padding.Bottom);
    }

    private void Services_BusyChanged(object? sender, EventArgs e) =>
        SettingsRoot.IsHitTestVisible = !_services.Busy.IsBusy;

    private void SettingsWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_services.Busy.IsUpdate)
        {
            args.Cancel = true;
        }
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        _services.TextRemoval.Save();
        _services.Busy.Changed -= Services_BusyChanged;
        _services.TimeShift.Changed -= TimeShift_Changed;
        _services.TextRemoval.Changed -= TextRemoval_Changed;
    }

    private void TimeShift_Changed(object? sender, EventArgs e)
    {
        if (!_updatingShift)
        {
            LoadTimeShift();
        }
    }

    private void TextRemoval_Changed(object? sender, EventArgs e)
    {
        if (!_updatingRules)
        {
            LoadTextRules();
        }
    }

    private void LoadTextRules()
    {
        _updatingRules = true;

        var hasFolder = _services.TextRemoval.Folder is not null;
        FolderRemovalBox.IsEnabled = hasFolder;
        FolderRemovalBox.Text = TextRuleList.Format(_services.TextRemoval.FolderRules);
        GlobalRemovalBox.Text = TextRuleList.Format(_services.TextRemoval.GlobalRules);

        _updatingRules = false;
    }

    private void FolderRemovalBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_rulesReady || _updatingRules)
        {
            return;
        }

        _updatingRules = true;
        _services.TextRemoval.UpdateFolderRules(TextRuleList.Parse(FolderRemovalBox.Text));
        _updatingRules = false;
    }

    private void GlobalRemovalBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_rulesReady || _updatingRules)
        {
            return;
        }

        _updatingRules = true;
        _services.TextRemoval.UpdateGlobalRules(TextRuleList.Parse(GlobalRemovalBox.Text));
        _updatingRules = false;
    }

    private void RemovalBox_LostFocus(object sender, RoutedEventArgs e) =>
        _services.TextRemoval.Save();

    private async Task CheckVersionsAsync()
    {
        if (_isChecking || _isUpdating)
        {
            return;
        }

        _isChecking = true;
        ViewModel.BeginCheck();

        try
        {
            var local = await _services.FfmpegUpdates.ReadLocalAsync();
            if (local.Display is { Length: > 0 })
            {
                ViewModel.SetVersion(local.Display);
            }
            else
            {
                ViewModel.ShowVersionError();
            }

            if (local.ErrorMessage is not null)
            {
                _services.Log.Add(local.ErrorMessage, isError: true);
                return;
            }

            var check = await _services.FfmpegUpdates.CheckForUpdateAsync(local);
            _latestVersionDisplay = check.LatestDisplay ?? _latestVersionDisplay;

            if (check.UpdateAvailable)
            {
                ViewModel.ShowUpdateAvailable();
            }

            if (check.ErrorMessage is not null)
            {
                _services.Log.Add(check.ErrorMessage, isError: true);
            }
        }
        catch (Exception ex)
        {
            _services.Log.Add($"Couldn't check for ffmpeg updates: {ex.Message}", isError: true);
        }
        finally
        {
            _isChecking = false;
        }
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e) => _ = UpdateAsync();

    private async Task UpdateAsync()
    {
        if (_isUpdating || _isChecking || _services.Busy.IsBusy)
        {
            return;
        }

        _isUpdating = true;
        SettingsRoot.IsHitTestVisible = false;
        ViewModel.BeginUpdate();
        IDisposable? busy = null;

        try
        {
            busy = _services.Busy.Begin(isUpdate: true);
            var result = await _services.FfmpegUpdates.UpdateAsync(
                new UpdateProgress(this),
                _latestVersionDisplay ?? ViewModel.VersionText);

            if (result.WarningMessage is not null)
            {
                _services.Log.Add(result.WarningMessage, isError: true);
            }

            ViewModel.ShowUpdateSucceeded(result.Display);
        }
        catch (Exception ex)
        {
            _services.Log.Add($"Couldn't update ffmpeg: {ex.Message}", isError: true);
            ViewModel.ShowUpdateFailed();
        }
        finally
        {
            busy?.Dispose();
            _isUpdating = false;
            SettingsRoot.IsHitTestVisible = true;
        }
    }

    private void ApplyUpdateProgress(FfmpegInstallProgress progress)
    {
        if (_isUpdating)
        {
            ViewModel.ApplyUpdateProgress(progress);
        }
    }

    private void ShiftSignBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_shiftReady)
        {
            UpdateTimeShift();
        }
    }

    private void ShiftPart_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args) =>
        args.Cancel = args.NewText.Any(character => !char.IsAsciiDigit(character));

    private void ShiftPart_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_shiftReady || _updatingShift || sender is not TextBox box)
        {
            return;
        }

        UpdateTimeShift();

        if (box.Text.Length == box.MaxLength)
        {
            MoveToNextShiftPart(box);
        }
    }

    private void ShiftPart_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.SelectAll();
        }
    }

    private void ShiftPart_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        var part = PartOf(box);

        _updatingShift = true;
        box.Text = TimeShiftText.NormalizePart(part, box.Text);
        _updatingShift = false;

        UpdateTimeShift();
    }

    private void MoveToNextShiftPart(TextBox box)
    {
        var next = box == ShiftHoursBox ? ShiftMinutesBox
            : box == ShiftMinutesBox ? ShiftSecondsBox
            : box == ShiftSecondsBox ? ShiftMillisecondsBox
            : null;

        next?.Focus(FocusState.Programmatic);
    }

    private void LoadTimeShift()
    {
        _updatingShift = true;

        var hasFolder = _services.TimeShift.Folder is not null;
        ShiftSignBox.IsEnabled = hasFolder;
        ShiftHoursBox.IsEnabled = hasFolder;
        ShiftMinutesBox.IsEnabled = hasFolder;
        ShiftSecondsBox.IsEnabled = hasFolder;
        ShiftMillisecondsBox.IsEnabled = hasFolder;

        var fields = TimeShiftText.Format(_services.TimeShift.Shift);
        ShiftSignBox.SelectedIndex = fields.IsNegative ? 1 : 0;
        ShiftHoursBox.Text = fields.Hours;
        ShiftMinutesBox.Text = fields.Minutes;
        ShiftSecondsBox.Text = fields.Seconds;
        ShiftMillisecondsBox.Text = fields.Milliseconds;

        _updatingShift = false;
    }

    private void UpdateTimeShift()
    {
        if (_updatingShift)
        {
            return;
        }

        var fields = new TimeShiftFields(
            ShiftSignBox.SelectedIndex == 1,
            ShiftHoursBox.Text,
            ShiftMinutesBox.Text,
            ShiftSecondsBox.Text,
            ShiftMillisecondsBox.Text);

        _updatingShift = true;
        _services.TimeShift.SetShift(TimeShiftText.Parse(fields));
        _updatingShift = false;
    }

    private TimeShiftPart PartOf(TextBox box) =>
        box == ShiftHoursBox ? TimeShiftPart.Hours
        : box == ShiftMinutesBox ? TimeShiftPart.Minutes
        : box == ShiftSecondsBox ? TimeShiftPart.Seconds
        : box == ShiftMillisecondsBox ? TimeShiftPart.Milliseconds
        : throw new ArgumentOutOfRangeException(nameof(box), box.Name, "Unknown time-shift field.");

    private sealed class UpdateProgress(SettingsWindow window) : IProgress<FfmpegInstallProgress>
    {
        public void Report(FfmpegInstallProgress value)
        {
            if (window.DispatcherQueue.HasThreadAccess)
            {
                window.ApplyUpdateProgress(value);
            }
            else
            {
                window.DispatcherQueue.TryEnqueue(() => window.ApplyUpdateProgress(value));
            }
        }
    }
}

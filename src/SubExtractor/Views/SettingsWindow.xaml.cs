using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Services;
using SubExtractor.ViewModels;
using Windows.Graphics;

namespace SubExtractor.Views;

public sealed partial class SettingsWindow : Window
{
    private static readonly TimeSpan LocalVersionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RemoteVersionTimeout = TimeSpan.FromSeconds(15);

    private readonly AppServices _services;
    private bool _checkStarted;
    private bool _isChecking;
    private bool _isUpdating;
    private bool _shiftReady;
    private bool _updatingShift;
    private string? _latestVersionDisplay;

    public SettingsWindow(AppServices services, Window owner)
    {
        _services = services;
        ViewModel = new SettingsViewModel();
        InitializeComponent();
        LoadTimeShift();
        _shiftReady = true;
        WindowOwner.SetOwner(this, owner);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        AppWindow.Resize(new SizeInt32(560, 380));
        WindowOwner.CenterOnOwner(this, owner);

        _services.Busy.Changed += Services_BusyChanged;
        SettingsRoot.Loaded += SettingsWindow_Loaded;
        AppWindow.Closing += SettingsWindow_Closing;
        Closed += SettingsWindow_Closed;
    }

    public SettingsViewModel ViewModel { get; }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_checkStarted)
        {
            return;
        }

        _checkStarted = true;
        _ = CheckVersionsAsync();
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

    private void SettingsWindow_Closed(object sender, WindowEventArgs args) =>
        _services.Busy.Changed -= Services_BusyChanged;

    private void RetryButton_Click(object sender, RoutedEventArgs e) => _ = CheckVersionsAsync();

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
            var manager = _services.Ffmpeg;

            string? localOutput;
            using (var localCts = new CancellationTokenSource(LocalVersionTimeout))
            {
                localOutput = await manager.ReadLocalVersionAsync(localCts.Token);
            }

            var localVersion = FfmpegVersionParser.Parse(localOutput ?? "");
            ViewModel.SetVersion(localVersion.Display);

            string remoteVersion;
            using (var remoteCts = new CancellationTokenSource(RemoteVersionTimeout))
            {
                remoteVersion = await manager.GetLatestVersionAsync(remoteCts.Token);
            }

            var remoteNumeric = VersionComparison.ParseNumeric(remoteVersion);
            if (localVersion.Numeric is null || remoteNumeric is null)
            {
                ViewModel.ShowCannotCompare();
                return;
            }

            _latestVersionDisplay = remoteNumeric.ToString();

            if (VersionComparison.IsNewer(localOutput ?? "", remoteVersion))
            {
                ViewModel.ShowUpdateAvailable();
            }
            else
            {
                ViewModel.ShowUpToDate();
            }
        }
        catch (Exception)
        {
            ViewModel.ShowCheckFailed();
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
            await _services.Ffmpeg.InstallAsync(new UpdateProgress(this), CancellationToken.None);

            var display = _latestVersionDisplay ?? ViewModel.VersionText;
            try
            {
                using var localCts = new CancellationTokenSource(LocalVersionTimeout);
                var localOutput = await _services.Ffmpeg.ReadLocalVersionAsync(localCts.Token);
                var parsed = FfmpegVersionParser.Parse(localOutput ?? "");
                if (parsed.Display.Length > 0)
                {
                    display = parsed.Display;
                }
            }
            catch (Exception)
            {
                // NOTE: The install already succeeded; a failed version re-read must not report an update failure.
            }

            ViewModel.ShowUpdateSucceeded(display);
        }
        catch (Exception ex)
        {
            ViewModel.ShowUpdateFailed(ex.Message);
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

        var value = ParseShiftPart(box);
        if (box == ShiftMinutesBox || box == ShiftSecondsBox)
        {
            value = Math.Min(value, 59);
        }

        _updatingShift = true;
        box.Text = value.ToString(box.MaxLength == 3 ? "000" : "00");
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

        var shift = _services.SubtitleTimeShift;
        ShiftSignBox.SelectedIndex = shift < TimeSpan.Zero ? 1 : 0;

        var absolute = shift.Duration();
        ShiftHoursBox.Text = ((long)absolute.TotalHours).ToString("00");
        ShiftMinutesBox.Text = absolute.Minutes.ToString("00");
        ShiftSecondsBox.Text = absolute.Seconds.ToString("00");
        ShiftMillisecondsBox.Text = absolute.Milliseconds.ToString("000");

        _updatingShift = false;
    }

    private void UpdateTimeShift()
    {
        if (_updatingShift)
        {
            return;
        }

        var sign = ShiftSignBox.SelectedIndex == 1 ? -1 : 1;
        var shift = new TimeSpan(
            0,
            ParseShiftPart(ShiftHoursBox),
            ParseShiftPart(ShiftMinutesBox),
            ParseShiftPart(ShiftSecondsBox),
            ParseShiftPart(ShiftMillisecondsBox));

        _services.SubtitleTimeShift = sign < 0 ? shift.Negate() : shift;
    }

    private static int ParseShiftPart(TextBox box) =>
        int.TryParse(box.Text, out var value) ? value : 0;

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

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.ViewModels;

namespace SubExtractor.Services;

public sealed class FfmpegBootstrapper
{
    private readonly FfmpegManager _manager;
    private readonly AppBusyState _busy;
    private readonly OperationCoordinator _operations;
    private readonly ActivityLog _log;

    public FfmpegBootstrapper(
        FfmpegManager manager,
        AppBusyState busy,
        OperationCoordinator operations,
        ActivityLog log)
    {
        _manager = manager;
        _busy = busy;
        _operations = operations;
        _log = log;
    }

    public async Task<bool> EnsureInstalledAsync(Window owner)
    {
        using var busy = _busy.Begin();

        if (await FfmpegHealthCheck.IsUsableAsync(_manager))
        {
            return true;
        }

        while (true)
        {
            _log.BeginOperation("Downloading ffmpeg");
            _log.SetProgress(0);

            var progress = new InstallProgress(_log, owner.DispatcherQueue);

            using (var operation = _operations.Begin())
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
            {
                EventHandler cancelHandler = (_, _) => cancellation.Cancel();
                _log.CancelRequested += cancelHandler;

                try
                {
                    await _manager.InstallAsync(progress, cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    _log.Add("FFmpeg installation cancelled.");
                    _log.EndOperation();
                    return false;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    _log.Add($"FFmpeg installation failed: {ex.Message}", isError: true);
                    _log.EndOperation();
                    await ShowDataNotWritableAsync(owner);
                    return false;
                }
                catch (Exception ex)
                {
                    var message = ex is OperationCanceledException
                        ? "The download timed out. Check your connection and try again."
                        : ex.Message;
                    _log.Add(message, isError: true);
                    _log.EndOperation();
                    var choice = await ShowRetryDialogAsync(owner, message);

                    if (choice != ContentDialogResult.Primary)
                    {
                        return false;
                    }

                    continue;
                }
                finally
                {
                    _log.CancelRequested -= cancelHandler;
                }
            }

            _log.Add("FFmpeg is ready.");
            _log.EndOperation();
            return true;
        }
    }

    private static async Task ShowDataNotWritableAsync(Window owner)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = owner.Content.XamlRoot,
            Title = "Cannot write to the Data folder",
            Content = "SubExtractor stores ffmpeg in the Data folder next to the application. "
                + "Make sure the application folder is writable and try again.",
            CloseButtonText = "Close",
        };

        await dialog.ShowAsync();
    }

    private static async Task<ContentDialogResult> ShowRetryDialogAsync(
        Window owner,
        string details)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = owner.Content.XamlRoot,
            Title = "Download failed",
            Content = details,
            PrimaryButtonText = "Retry",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync();
    }

    private sealed class InstallProgress(ActivityLog log, DispatcherQueue dispatcher)
        : IProgress<FfmpegInstallProgress>
    {
        private LogEntry? _downloadEntry;

        public void Report(FfmpegInstallProgress value)
        {
            if (dispatcher.HasThreadAccess)
            {
                Apply(value);
            }
            else
            {
                dispatcher.TryEnqueue(() => Apply(value));
            }
        }

        private void Apply(FfmpegInstallProgress value)
        {
            switch (value.Stage)
            {
                case FfmpegInstallStage.Downloading:
                    var message = value.Percent is { } percent
                        ? $"Downloading ffmpeg — {percent:0}%"
                        : "Downloading ffmpeg";

                    if (_downloadEntry is null)
                    {
                        _downloadEntry = log.Add(message);
                    }
                    else
                    {
                        log.Update(_downloadEntry, message);
                    }

                    log.SetProgress(value.Percent);
                    break;

                case FfmpegInstallStage.Verifying:
                    log.Add("Verifying the download");
                    log.SetProgress(null);
                    break;

                case FfmpegInstallStage.Extracting:
                    log.Add("Extracting ffmpeg");
                    log.SetProgress(null);
                    break;

                case FfmpegInstallStage.Installing:
                    log.Add("Installing ffmpeg");
                    log.SetProgress(null);
                    break;

                case FfmpegInstallStage.Done:
                    log.SetProgress(100);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value.Stage), value.Stage, "Unknown ffmpeg install stage.");
            }
        }
    }
}

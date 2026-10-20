using System.Reflection.PortableExecutable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.ViewModels;
using SubExtractor.Views;

namespace SubExtractor.Services;

public sealed class FfmpegBootstrapper
{
    private const string DownloadingStage = "Downloading";
    private const string VerifyingStage = "Verifying";
    private const string ExtractingStage = "Extracting";
    private const string InstallingStage = "Installing";
    private const string DoneStage = "Done";

    private static readonly TimeSpan VersionCheckTimeout = TimeSpan.FromSeconds(10);

    private readonly FfmpegManager _manager;
    private readonly AppBusyState _busy;
    private readonly OperationCoordinator _operations;

    public FfmpegBootstrapper(
        FfmpegManager manager,
        AppBusyState busy,
        OperationCoordinator operations)
    {
        _manager = manager;
        _busy = busy;
        _operations = operations;
    }

    public async Task<bool> EnsureInstalledAsync(Window owner)
    {
        using var busy = _busy.Begin();

        if (await IsUsableAsync())
        {
            return true;
        }

        while (true)
        {
            var progressWindow = new ProgressWindow("Downloading ffmpeg", owner);
            progressWindow.Activate();
            await progressWindow.WaitForLoadedAsync();

            var progress = new InstallProgress(progressWindow);

            using (var operation = _operations.Begin())
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
            {
                progressWindow.CancelRequested += (_, _) => cancellation.Cancel();

                try
                {
                    await _manager.InstallAsync(progress, cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    progressWindow.AllowClose();
                    progressWindow.Close();
                    return false;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    await ShowDataNotWritableAsync(progressWindow);
                    progressWindow.AllowClose();
                    progressWindow.Close();
                    return false;
                }
                catch (Exception ex)
                {
                    var message = ex is OperationCanceledException
                        ? "The download timed out. Check your connection and try again."
                        : ex.Message;
                    progressWindow.AddLog(message, isError: true);
                    var choice = await ShowRetryDialogAsync(progressWindow, message);
                    progressWindow.AllowClose();
                    progressWindow.Close();

                    if (choice != ContentDialogResult.Primary)
                    {
                        return false;
                    }

                    continue;
                }
            }

            progressWindow.Finish("FFmpeg is ready.");
            await progressWindow.WaitForCloseAsync();
            return true;
        }
    }

    private async Task<bool> IsUsableAsync()
    {
        if (!_manager.IsInstalled)
        {
            return false;
        }

        if (!IsPortableExecutable(_manager.FfmpegPath) || !IsPortableExecutable(_manager.FfprobePath))
        {
            return false;
        }

        using var timeout = new CancellationTokenSource(VersionCheckTimeout);

        try
        {
            var ffmpeg = _manager.ReadLocalVersionAsync(timeout.Token);
            var ffprobe = _manager.ReadLocalProbeVersionAsync(timeout.Token);
            var versions = await Task.WhenAll(ffmpeg, ffprobe);
            return versions[0] is not null && versions[1] is not null;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static bool IsPortableExecutable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.PEHeader is not null
                && reader.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.ExecutableImage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    private static async Task ShowDataNotWritableAsync(ProgressWindow progressWindow)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = progressWindow.Content.XamlRoot,
            Title = "Cannot write to the Data folder",
            Content = "SubExtractor stores ffmpeg in the Data folder next to the application. "
                + "Make sure the application folder is writable and try again.",
            CloseButtonText = "Close",
        };

        await dialog.ShowAsync();
    }

    private static async Task<ContentDialogResult> ShowRetryDialogAsync(
        ProgressWindow progressWindow,
        string details)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = progressWindow.Content.XamlRoot,
            Title = "Download failed",
            Content = details,
            PrimaryButtonText = "Retry",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync();
    }

    private sealed class InstallProgress(ProgressWindow progressWindow) : IProgress<FfmpegInstallProgress>
    {
        private LogEntry? _downloadEntry;

        public void Report(FfmpegInstallProgress value)
        {
            if (progressWindow.DispatcherQueue.HasThreadAccess)
            {
                Apply(value);
            }
            else
            {
                progressWindow.DispatcherQueue.TryEnqueue(() => Apply(value));
            }
        }

        private void Apply(FfmpegInstallProgress value)
        {
            switch (value.Stage)
            {
                case DownloadingStage:
                    var message = value.Percent is { } percent
                        ? $"Downloading ffmpeg — {percent:0}%"
                        : "Downloading ffmpeg";

                    if (_downloadEntry is null)
                    {
                        _downloadEntry = progressWindow.AddLog(message);
                    }
                    else
                    {
                        progressWindow.UpdateLog(_downloadEntry, message);
                    }

                    progressWindow.SetOverall(value.Percent);
                    break;

                case VerifyingStage:
                    progressWindow.AddLog("Verifying the download");
                    progressWindow.SetOverall(null);
                    break;

                case ExtractingStage:
                    progressWindow.AddLog("Extracting ffmpeg");
                    progressWindow.SetOverall(null);
                    break;

                case InstallingStage:
                    progressWindow.AddLog("Installing ffmpeg");
                    progressWindow.SetOverall(null);
                    break;

                case DoneStage:
                    progressWindow.SetOverall(100);
                    break;
            }
        }
    }
}

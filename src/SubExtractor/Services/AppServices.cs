using SubExtractor.Core.Extraction;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Ffprobe;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Scanning;
using SubExtractor.Core.Shell;

namespace SubExtractor.Services;

public sealed class AppServices
{
    public AppServices()
    {
        DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var downloader = new HttpFfmpegDownloader(http);
        var processRunner = new OffThreadProcessRunner(new ProcessRunner());

        Ffmpeg = new FfmpegManager(DataDirectory, downloader, processRunner);
        Scanner = new ScanService(new FfprobeService(processRunner, Ffmpeg.FfprobePath));
        Extractor = new ExtractionService(processRunner, Ffmpeg.FfmpegPath);
        OverwriteRecycler = new OverwriteRecycler(new FileRecycler());
        Busy = new AppBusyState();
        Operations = new OperationCoordinator();
        Log = new ActivityLog();
        TimeShift = new FolderTimeShift(new SubtitleShiftStore(DataDirectory));
        TimeShift.PersistenceFailed += (_, exception) =>
            Log.Add($"Couldn't access saved subtitle time shifts: {exception.Message}", isError: true);
        TextRemoval = new TextRemovalSettings(new SubtitleTextStore(DataDirectory));
        TextRemoval.PersistenceFailed += (_, exception) =>
            Log.Add($"Couldn't access saved text removal rules: {exception.Message}", isError: true);
        Bootstrapper = new FfmpegBootstrapper(Ffmpeg, Busy, Operations, Log);
        FolderOpener = new FolderOpener();
    }

    public string DataDirectory { get; }

    public FfmpegManager Ffmpeg { get; }

    public ScanService Scanner { get; }

    public ExtractionService Extractor { get; }

    public OverwriteRecycler OverwriteRecycler { get; }

    public AppBusyState Busy { get; }

    public OperationCoordinator Operations { get; }

    public ActivityLog Log { get; }

    public FolderTimeShift TimeShift { get; }

    public TextRemovalSettings TextRemoval { get; }

    public FfmpegBootstrapper Bootstrapper { get; }

    public FolderOpener FolderOpener { get; }

    private sealed class OffThreadProcessRunner(IProcessRunner inner) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            RunAsync(executablePath, arguments, onStandardOutputLine: null, cancellationToken);

        public Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            Action<string>? onStandardOutputLine,
            CancellationToken cancellationToken)
        {
            // WHY: Starting a console process on the UI thread crashes the WinUI dispatcher.
            return Task.Run(
                () => inner.RunAsync(
                    executablePath, arguments, onStandardOutputLine, cancellationToken),
                cancellationToken);
        }
    }
}

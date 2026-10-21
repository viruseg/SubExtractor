using SubExtractor.Core.Extraction;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Ffprobe;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Scanning;

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
        Busy = new AppBusyState();
        Operations = new OperationCoordinator();
        Log = new ActivityLog();
        Bootstrapper = new FfmpegBootstrapper(Ffmpeg, Busy, Operations, Log);
    }

    public string DataDirectory { get; }

    public FfmpegManager Ffmpeg { get; }

    public ScanService Scanner { get; }

    public ExtractionService Extractor { get; }

    public AppBusyState Busy { get; }

    public OperationCoordinator Operations { get; }

    public ActivityLog Log { get; }

    public FfmpegBootstrapper Bootstrapper { get; }

    private sealed class OffThreadProcessRunner(IProcessRunner inner) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            // WHY: Starting a console process on the UI thread crashes the WinUI dispatcher.
            return Task.Run(
                () => inner.RunAsync(executablePath, arguments, cancellationToken),
                cancellationToken);
        }
    }
}

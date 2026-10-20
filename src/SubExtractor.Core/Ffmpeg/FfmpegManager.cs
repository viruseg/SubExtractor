using System.IO.Compression;
using System.Security.Cryptography;
using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Ffmpeg;

public sealed class FfmpegManager
{
    private const string DownloadingStage = "Downloading";
    private const string VerifyingStage = "Verifying";
    private const string ExtractingStage = "Extracting";
    private const string InstallingStage = "Installing";
    private const string DoneStage = "Done";

    private static readonly Uri ReleaseVersionUri =
        new("https://www.gyan.dev/ffmpeg/builds/release-version");
    private static readonly Uri ArchiveUri =
        new("https://www.gyan.dev/ffmpeg/builds/release-essentials.zip");
    private static readonly Uri ChecksumUri =
        new("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256");

    private readonly string _dataDirectory;
    private readonly string _ffmpegDirectory;
    private readonly IFfmpegDownloader _downloader;
    private readonly IProcessRunner _processRunner;

    public FfmpegManager(string dataDirectory, IFfmpegDownloader downloader, IProcessRunner processRunner)
    {
        _dataDirectory = dataDirectory;
        _ffmpegDirectory = Path.Combine(dataDirectory, "ffmpeg");
        _downloader = downloader;
        _processRunner = processRunner;
    }

    public string FfmpegPath => Path.Combine(_ffmpegDirectory, "ffmpeg.exe");

    public string FfprobePath => Path.Combine(_ffmpegDirectory, "ffprobe.exe");

    public bool IsInstalled => File.Exists(FfmpegPath) && File.Exists(FfprobePath);

    public Task<string?> ReadLocalVersionAsync(CancellationToken cancellationToken)
        => ReadVersionAsync(FfmpegPath, cancellationToken);

    public Task<string?> ReadLocalProbeVersionAsync(CancellationToken cancellationToken)
        => ReadVersionAsync(FfprobePath, cancellationToken);

    public async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        var version = await _downloader
            .GetStringAsync(ReleaseVersionUri, cancellationToken)
            .ConfigureAwait(false);
        return version.Trim();
    }

    public async Task InstallAsync(
        IProgress<FfmpegInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_dataDirectory);

        var archivePath = Path.Combine(_dataDirectory, $"ffmpeg-download-{Guid.NewGuid():N}.tmp");
        var stagingPath = Path.Combine(_dataDirectory, $"ffmpeg-staging-{Guid.NewGuid():N}");
        var pendingTargets = new List<string>();

        try
        {
            progress?.Report(new FfmpegInstallProgress(DownloadingStage, null));

            IProgress<DownloadProgress>? downloadProgress = progress is null
                ? null
                : new InstallProgressReporter(progress);

            await using (var archive = new FileStream(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                await _downloader
                    .DownloadAsync(ArchiveUri, archive, downloadProgress, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new FfmpegInstallProgress(VerifyingStage, null));

            var expectedChecksum = ChecksumParser.Parse(
                    await _downloader.GetStringAsync(ChecksumUri, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidOperationException(
                    "The ffmpeg checksum file did not contain a SHA-256 hash.");

            var actualChecksum = await ComputeSha256Async(archivePath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The downloaded ffmpeg archive has SHA-256 {actualChecksum}, expected {expectedChecksum}.");
            }

            progress?.Report(new FfmpegInstallProgress(ExtractingStage, null));
            ZipFile.ExtractToDirectory(archivePath, stagingPath);

            var stagedFfmpeg = FindSingleExecutable(stagingPath, "ffmpeg.exe");
            var stagedFfprobe = FindSingleExecutable(stagingPath, "ffprobe.exe");

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new FfmpegInstallProgress(InstallingStage, null));
            Directory.CreateDirectory(_ffmpegDirectory);

            var pendingFfmpeg = Path.Combine(_ffmpegDirectory, $"ffmpeg-{Guid.NewGuid():N}.tmp");
            var pendingFfprobe = Path.Combine(_ffmpegDirectory, $"ffprobe-{Guid.NewGuid():N}.tmp");
            pendingTargets.Add(pendingFfmpeg);
            pendingTargets.Add(pendingFfprobe);

            File.Move(stagedFfmpeg, pendingFfmpeg);
            File.Move(stagedFfprobe, pendingFfprobe);
            File.Move(pendingFfmpeg, FfmpegPath, overwrite: true);
            File.Move(pendingFfprobe, FfprobePath, overwrite: true);
            pendingTargets.Clear();

            progress?.Report(new FfmpegInstallProgress(DoneStage, null));
        }
        finally
        {
            foreach (var pendingTarget in pendingTargets)
            {
                TryDeleteFile(pendingTarget);
            }

            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingPath);
        }
    }

    private async Task<string?> ReadVersionAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner
                .RunAsync(executablePath, ["-version"], cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0 ? result.StandardOutput : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FindSingleExecutable(string stagingPath, string fileName)
    {
        var matches = Directory.GetFiles(stagingPath, fileName, SearchOption.AllDirectories);
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"The ffmpeg archive must contain exactly one '{fileName}', but {matches.Length} were found.");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class InstallProgressReporter(IProgress<FfmpegInstallProgress> target)
        : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
            => target.Report(new FfmpegInstallProgress(DownloadingStage, value.Percent));
    }
}

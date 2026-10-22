namespace SubExtractor.Core.Ffmpeg;

public sealed record FfmpegLocalVersionResult(
    string? Display,
    string? RawOutput,
    string? ErrorMessage);

public sealed record FfmpegUpdateCheckResult(
    bool UpdateAvailable,
    string? LatestDisplay,
    string? ErrorMessage);

public sealed record FfmpegUpdateResult(string Display, string? WarningMessage);

public sealed class FfmpegUpdateService(FfmpegManager manager)
{
    private static readonly TimeSpan LocalVersionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RemoteVersionTimeout = TimeSpan.FromSeconds(15);

    public async Task<FfmpegLocalVersionResult> ReadLocalAsync()
    {
        string? output;

        try
        {
            using var timeout = new CancellationTokenSource(LocalVersionTimeout);
            output = await manager.ReadLocalVersionAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new FfmpegLocalVersionResult(
                null, null, $"Couldn't read the local ffmpeg version: {ex.Message}");
        }

        var parsed = FfmpegVersionParser.Parse(output ?? "");
        return parsed.Display.Length == 0
            ? new FfmpegLocalVersionResult(
                null, output, "Couldn't read the local ffmpeg version.")
            : new FfmpegLocalVersionResult(parsed.Display, output, null);
    }

    public async Task<FfmpegUpdateCheckResult> CheckForUpdateAsync(
        FfmpegLocalVersionResult local)
    {
        string remoteVersion;

        try
        {
            using var timeout = new CancellationTokenSource(RemoteVersionTimeout);
            remoteVersion = await manager.GetLatestVersionAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new FfmpegUpdateCheckResult(
                false, null, $"Couldn't check for ffmpeg updates: {ex.Message}");
        }

        var localVersion = FfmpegVersionParser.Parse(local.RawOutput ?? "");
        var remoteNumeric = VersionComparison.ParseNumeric(remoteVersion);
        if (localVersion.Numeric is null || remoteNumeric is null)
        {
            return new FfmpegUpdateCheckResult(
                false,
                null,
                $"Couldn't compare ffmpeg versions: local '{localVersion.Display}', " +
                $"server '{remoteVersion}'.");
        }

        return new FfmpegUpdateCheckResult(
            VersionComparison.IsNewer(local.RawOutput ?? "", remoteVersion),
            remoteNumeric.ToString(),
            null);
    }

    public async Task<FfmpegUpdateResult> UpdateAsync(
        IProgress<FfmpegInstallProgress>? progress,
        string fallbackDisplay)
    {
        await manager.InstallAsync(progress, CancellationToken.None).ConfigureAwait(false);

        try
        {
            using var timeout = new CancellationTokenSource(LocalVersionTimeout);
            var output = await manager.ReadLocalVersionAsync(timeout.Token)
                .ConfigureAwait(false);
            var parsed = FfmpegVersionParser.Parse(output ?? "");

            return parsed.Display.Length > 0
                ? new FfmpegUpdateResult(parsed.Display, null)
                : new FfmpegUpdateResult(
                    fallbackDisplay,
                    "Couldn't read the local ffmpeg version after updating.");
        }
        catch (Exception ex)
        {
            return new FfmpegUpdateResult(
                fallbackDisplay,
                $"Couldn't read the local ffmpeg version after updating: {ex.Message}");
        }
    }
}

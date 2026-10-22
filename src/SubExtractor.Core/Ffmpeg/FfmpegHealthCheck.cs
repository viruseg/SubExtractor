using System.Reflection.PortableExecutable;

namespace SubExtractor.Core.Ffmpeg;

public static class FfmpegHealthCheck
{
    private static readonly TimeSpan VersionCheckTimeout = TimeSpan.FromSeconds(10);

    public static async Task<bool> IsUsableAsync(FfmpegManager manager)
    {
        if (!manager.IsInstalled)
        {
            return false;
        }

        if (!IsPortableExecutable(manager.FfmpegPath) ||
            !IsPortableExecutable(manager.FfprobePath))
        {
            return false;
        }

        using var timeout = new CancellationTokenSource(VersionCheckTimeout);

        try
        {
            var ffmpeg = manager.ReadLocalVersionAsync(timeout.Token);
            var ffprobe = manager.ReadLocalProbeVersionAsync(timeout.Token);
            var versions = await Task.WhenAll(ffmpeg, ffprobe).ConfigureAwait(false);
            return versions[0] is not null && versions[1] is not null;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public static bool IsPortableExecutable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.PEHeader is not null
                && reader.PEHeaders.CoffHeader.Characteristics
                    .HasFlag(Characteristics.ExecutableImage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or BadImageFormatException)
        {
            return false;
        }
    }
}

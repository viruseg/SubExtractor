namespace SubExtractor.Core.Ffmpeg;

public interface IFfmpegDownloader
{
    Task DownloadAsync(
        Uri uri,
        Stream destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken);

    Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken);
}

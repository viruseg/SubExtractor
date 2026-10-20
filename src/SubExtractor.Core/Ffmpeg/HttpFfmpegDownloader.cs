namespace SubExtractor.Core.Ffmpeg;

public sealed class HttpFfmpegDownloader : IFfmpegDownloader
{
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient = new();

    public async Task DownloadAsync(
        Uri uri,
        Stream destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[BufferSize];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination
                .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            received += read;
            progress?.Report(new DownloadProgress(received, totalBytes));
        }
    }

    public async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(uri, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

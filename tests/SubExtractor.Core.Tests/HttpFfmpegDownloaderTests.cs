using System.Net;
using SubExtractor.Core.Ffmpeg;

namespace SubExtractor.Core.Tests;

public sealed class HttpFfmpegDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_streams_content_to_destination_and_reports_progress()
    {
        var payload = Enumerable.Range(0, 200_000).Select(i => (byte)i).ToArray();
        var uri = new Uri("https://example.test/ffmpeg.zip");
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        });
        using var client = new HttpClient(handler);
        var downloader = new HttpFfmpegDownloader(client);
        using var destination = new MemoryStream();
        var reports = new List<DownloadProgress>();

        await downloader.DownloadAsync(
            uri,
            destination,
            new SyncProgress<DownloadProgress>(reports.Add),
            CancellationToken.None);

        Assert.Equal(payload, destination.ToArray());
        Assert.Equal(uri, handler.LastRequestUri);
        Assert.NotEmpty(reports);
        Assert.All(reports, report => Assert.Equal(payload.Length, report.TotalBytes));
        Assert.Equal(payload.Length, reports[^1].BytesReceived);
        Assert.Equal(100d, reports[^1].Percent!.Value);
    }

    [Fact]
    public async Task DownloadAsync_throws_and_writes_nothing_on_error_status()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var downloader = new HttpFfmpegDownloader(client);
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync(
                new Uri("https://example.test/missing.zip"),
                destination,
                progress: null,
                CancellationToken.None));

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task GetStringAsync_returns_the_response_body()
    {
        var uri = new Uri("https://example.test/release-version");
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("9.0.3\n"),
        });
        using var client = new HttpClient(handler);
        var downloader = new HttpFfmpegDownloader(client);

        Assert.Equal(
            "9.0.3\n",
            await downloader.GetStringAsync(uri, CancellationToken.None));
        Assert.Equal(uri, handler.LastRequestUri);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

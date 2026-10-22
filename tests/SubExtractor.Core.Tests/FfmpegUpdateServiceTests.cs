using System.IO.Compression;
using System.Security.Cryptography;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class FfmpegUpdateServiceTests
{
    [Fact]
    public async Task ReadLocalAsync_returns_the_parsed_display()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2");

        var result = await service.ReadLocalAsync();

        Assert.Equal("9.0.2", result.Display);
        Assert.Equal("ffmpeg version 9.0.2", result.RawOutput);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ReadLocalAsync_reports_an_unreadable_version()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, null, exitCode: 1);

        var result = await service.ReadLocalAsync();

        Assert.Null(result.Display);
        Assert.Equal("Couldn't read the local ffmpeg version.", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_a_newer_remote_version()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2", remoteVersion: "9.0.3");
        var local = await service.ReadLocalAsync();

        var result = await service.CheckForUpdateAsync(local);

        Assert.True(result.UpdateAvailable);
        Assert.Equal("9.0.3", result.LatestDisplay);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_no_update_for_the_same_version()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2", remoteVersion: "9.0.2");
        var local = await service.ReadLocalAsync();

        var result = await service.CheckForUpdateAsync(local);

        Assert.False(result.UpdateAvailable);
        Assert.Equal("9.0.2", result.LatestDisplay);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_a_remote_failure()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2", remoteFailure: true);
        var local = await service.ReadLocalAsync();

        var result = await service.CheckForUpdateAsync(local);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestDisplay);
        Assert.StartsWith("Couldn't check for ffmpeg updates: network down", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_incomparable_versions()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version nightly", remoteVersion: "9.0.3");
        var local = await service.ReadLocalAsync();

        var result = await service.CheckForUpdateAsync(local);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestDisplay);
        Assert.Equal(
            "Couldn't compare ffmpeg versions: local 'nightly', server '9.0.3'.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_returns_the_version_read_after_installing()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.3");

        var result = await service.UpdateAsync(null, "9.0.2");

        Assert.Equal("9.0.3", result.Display);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public async Task UpdateAsync_falls_back_when_the_version_cannot_be_read()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, null, exitCode: 1);

        var result = await service.UpdateAsync(null, "9.0.2");

        Assert.Equal("9.0.2", result.Display);
        Assert.Equal(
            "Couldn't read the local ffmpeg version after updating.",
            result.WarningMessage);
    }

    [Fact]
    public async Task UpdateAsync_propagates_install_failures()
    {
        using var temp = new TempDirectory();
        var service = new FfmpegUpdateService(new FfmpegManager(
            Path.Combine(temp.Path, "Data"),
            new ThrowingDownloader(),
            new FakeProcessRunner(new ProcessResult(0, "", ""))));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.UpdateAsync(null, "9.0.2"));
    }

    private static FfmpegUpdateService CreateService(
        TempDirectory temp,
        string? localOutput,
        int exitCode = 0,
        string? remoteVersion = "9.0.2",
        bool remoteFailure = false)
    {
        var archive = CreateArchive();
        var downloader = remoteFailure
            ? (IFfmpegDownloader)new ThrowingDownloader()
            : new VersionDownloader(remoteVersion!, archive, Sha256(archive));
        var runner = new FakeProcessRunner(new ProcessResult(exitCode, localOutput ?? "", ""));
        var manager = new FfmpegManager(Path.Combine(temp.Path, "Data"), downloader, runner);
        return new FfmpegUpdateService(manager);
    }

    private static byte[] CreateArchive()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffmpeg.exe", "FFMPEG");
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffprobe.exe", "FFPROBE");
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string Sha256(byte[] archive) =>
        Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

    private sealed class VersionDownloader(string version, byte[] archive, string checksum)
        : IFfmpegDownloader
    {
        public Task DownloadAsync(
            Uri uri,
            Stream destination,
            IProgress<DownloadProgress>? progress,
            CancellationToken ct)
        {
            destination.Write(archive);
            progress?.Report(new DownloadProgress(archive.Length, archive.Length));
            return Task.CompletedTask;
        }

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
            Task.FromResult(uri.AbsolutePath.EndsWith(".sha256") ? checksum : version);
    }

    private sealed class ThrowingDownloader : IFfmpegDownloader
    {
        public Task DownloadAsync(
            Uri uri,
            Stream destination,
            IProgress<DownloadProgress>? progress,
            CancellationToken ct) => throw new HttpRequestException("network down");

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
            throw new HttpRequestException("network down");
    }
}

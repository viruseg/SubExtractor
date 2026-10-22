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
    public async Task CheckForUpdateAsync_returns_the_local_error_before_checking_remote()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2", remoteFailure: true);
        var local = new FfmpegLocalVersionResult(
            null, null, "Couldn't read the local ffmpeg version.");

        var result = await service.CheckForUpdateAsync(local);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestDisplay);
        Assert.Equal("Couldn't read the local ffmpeg version.", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_uses_the_local_display_in_the_comparison_error()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp, "ffmpeg version 9.0.2", remoteVersion: "9.0.3");
        var local = new FfmpegLocalVersionResult("custom", "ffmpeg version nightly", null);

        var result = await service.CheckForUpdateAsync(local);

        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestDisplay);
        Assert.Equal(
            "Couldn't compare ffmpeg versions: local 'custom', server '9.0.3'.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task ReadLocalAsync_reports_a_process_exception()
    {
        using var temp = new TempDirectory();
        var service = new FfmpegUpdateService(new FfmpegManager(
            temp.Path,
            new ThrowingFfmpegDownloader(),
            new FakeProcessRunner((_, _, _) =>
                throw new OperationCanceledException("local boom"))));

        var result = await service.ReadLocalAsync();

        Assert.Null(result.Display);
        Assert.Equal(
            "Couldn't read the local ffmpeg version: local boom",
            result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_warns_when_the_post_install_read_throws()
    {
        using var temp = new TempDirectory();
        var archive = FfmpegTestData.CreateArchive();
        var service = new FfmpegUpdateService(new FfmpegManager(
            Path.Combine(temp.Path, "Data"),
            new FakeFfmpegDownloader("9.0.2", archive, FfmpegTestData.Sha256(archive)),
            new FakeProcessRunner((_, _, _) =>
                throw new OperationCanceledException("local boom"))));

        var result = await service.UpdateAsync(null, "9.0.2");

        Assert.Equal("9.0.2", result.Display);
        Assert.Equal(
            "Couldn't read the local ffmpeg version after updating: local boom",
            result.WarningMessage);
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
            new ThrowingFfmpegDownloader(),
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
        var archive = FfmpegTestData.CreateArchive();
        var downloader = remoteFailure
            ? (IFfmpegDownloader)new ThrowingFfmpegDownloader()
            : new FakeFfmpegDownloader(
                remoteVersion!, archive, FfmpegTestData.Sha256(archive));
        var runner = new FakeProcessRunner(new ProcessResult(exitCode, localOutput ?? "", ""));
        var manager = new FfmpegManager(Path.Combine(temp.Path, "Data"), downloader, runner);
        return new FfmpegUpdateService(manager);
    }

}

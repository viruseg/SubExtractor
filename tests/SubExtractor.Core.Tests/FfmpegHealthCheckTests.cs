using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class FfmpegHealthCheckTests
{
    [Fact]
    public void IsPortableExecutable_returns_false_for_a_missing_file()
    {
        using var temp = new TempDirectory();

        Assert.False(FfmpegHealthCheck.IsPortableExecutable(
            Path.Combine(temp.Path, "missing.exe")));
    }

    [Fact]
    public void IsPortableExecutable_returns_false_for_a_non_pe_file()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "ffmpeg.exe");
        File.WriteAllText(path, "not a pe");

        Assert.False(FfmpegHealthCheck.IsPortableExecutable(path));
    }

    [Fact]
    public async Task IsUsableAsync_returns_false_when_not_installed()
    {
        using var temp = new TempDirectory();
        var manager = new FfmpegManager(
            temp.Path,
            new StubDownloader(),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        Assert.False(await FfmpegHealthCheck.IsUsableAsync(manager));
    }

    [Fact]
    public async Task IsUsableAsync_returns_false_when_files_are_not_pe()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "ffmpeg");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ffmpeg.exe"), "not a pe");
        File.WriteAllText(Path.Combine(directory, "ffprobe.exe"), "not a pe");
        var manager = new FfmpegManager(
            temp.Path,
            new StubDownloader(),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        Assert.False(await FfmpegHealthCheck.IsUsableAsync(manager));
    }

    [WindowsFact]
    public async Task IsUsableAsync_returns_true_when_both_versions_are_readable()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "ffmpeg");
        Directory.CreateDirectory(directory);
        var pe = Environment.ProcessPath
            ?? typeof(FfmpegHealthCheckTests).Assembly.Location;
        File.Copy(pe, Path.Combine(directory, "ffmpeg.exe"));
        File.Copy(pe, Path.Combine(directory, "ffprobe.exe"));
        var manager = new FfmpegManager(
            temp.Path,
            new StubDownloader(),
            new FakeProcessRunner(new ProcessResult(0, "ffmpeg version 9.0.2", "")));

        Assert.True(await FfmpegHealthCheck.IsUsableAsync(manager));
    }

    [WindowsFact]
    public async Task IsUsableAsync_returns_false_when_one_version_read_fails()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "ffmpeg");
        Directory.CreateDirectory(directory);
        var pe = Environment.ProcessPath
            ?? typeof(FfmpegHealthCheckTests).Assembly.Location;
        File.Copy(pe, Path.Combine(directory, "ffmpeg.exe"));
        File.Copy(pe, Path.Combine(directory, "ffprobe.exe"));
        Assert.True(FfmpegHealthCheck.IsPortableExecutable(
            Path.Combine(directory, "ffmpeg.exe")));
        Assert.True(FfmpegHealthCheck.IsPortableExecutable(
            Path.Combine(directory, "ffprobe.exe")));
        var manager = new FfmpegManager(
            temp.Path,
            new StubDownloader(),
            new FakeProcessRunner((path, _, _) =>
                path.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
                    ? new ProcessResult(1, "", "failed")
                    : new ProcessResult(0, "ffmpeg version 9.0.2", "")));

        Assert.False(await FfmpegHealthCheck.IsUsableAsync(manager));
    }

    private sealed class StubDownloader : IFfmpegDownloader
    {
        public Task DownloadAsync(
            Uri uri,
            Stream destination,
            IProgress<DownloadProgress>? progress,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class FfmpegManagerTests
{
    [Fact]
    public async Task Install_extracts_ffmpeg_and_ffprobe_and_cleans_staging()
    {
        using var temp = new TempDirectory();
        var archive = CreateArchive();
        var checksum = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        var manager = new FfmpegManager(
            Path.Combine(temp.Path, "Data"),
            new FakeDownloader(archive, checksum),
            new FakeProcessRunner(new ProcessResult(0, "ffmpeg version 9.0.2", "")));
        var stages = new List<string>();

        await manager.InstallAsync(
            new SyncProgress<FfmpegInstallProgress>(p => stages.Add(p.Stage)),
            CancellationToken.None);

        Assert.True(manager.IsInstalled);
        Assert.Equal("FFMPEG", File.ReadAllText(manager.FfmpegPath));
        Assert.Equal("FFPROBE", File.ReadAllText(manager.FfprobePath));
        Assert.Empty(Directory.GetFiles(Path.Combine(temp.Path, "Data"), "*.tmp", SearchOption.AllDirectories));
        Assert.Equal(["Downloading", "Verifying", "Extracting", "Installing", "Done"], stages.Distinct());
    }

    [Fact]
    public async Task Install_reports_download_percent()
    {
        using var temp = new TempDirectory();
        var archive = CreateArchive();
        var manager = new FfmpegManager(
            Path.Combine(temp.Path, "Data"),
            new FakeDownloader(archive, Sha256(archive)),
            new FakeProcessRunner(new ProcessResult(0, "", "")));
        var reports = new List<FfmpegInstallProgress>();

        await manager.InstallAsync(new SyncProgress<FfmpegInstallProgress>(reports.Add), CancellationToken.None);

        Assert.Contains(new FfmpegInstallProgress("Downloading", 100), reports);
        Assert.Equal(new FfmpegInstallProgress("Downloading", null), reports[0]);
    }

    [Fact]
    public async Task Install_replaces_existing_executables()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        File.WriteAllText(Path.Combine(ffmpegDir, "ffprobe.exe"), "OLD");
        var archive = CreateArchive();
        var manager = new FfmpegManager(data, new FakeDownloader(archive, Sha256(archive)),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await manager.InstallAsync(null, CancellationToken.None);

        Assert.Equal("FFMPEG", File.ReadAllText(manager.FfmpegPath));
        Assert.Equal("FFPROBE", File.ReadAllText(manager.FfprobePath));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task Install_keeps_existing_files_when_checksum_does_not_match()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        var manager = new FfmpegManager(data, new FakeDownloader(CreateArchive(), new string('0', 64)),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InstallAsync(null, CancellationToken.None));

        Assert.Equal("OLD", File.ReadAllText(Path.Combine(ffmpegDir, "ffmpeg.exe")));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task Install_keeps_existing_files_when_checksum_file_is_invalid()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        var manager = new FfmpegManager(data, new FakeDownloader(CreateArchive(), "not-a-checksum"),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InstallAsync(null, CancellationToken.None));

        Assert.Equal("OLD", File.ReadAllText(Path.Combine(ffmpegDir, "ffmpeg.exe")));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task Install_keeps_existing_files_when_download_fails()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        var manager = new FfmpegManager(data, new ThrowingDownloader(),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => manager.InstallAsync(null, CancellationToken.None));

        Assert.Equal("OLD", File.ReadAllText(Path.Combine(ffmpegDir, "ffmpeg.exe")));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task Install_cleans_partial_files_when_cancelled()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        using var cts = new CancellationTokenSource();
        var manager = new FfmpegManager(data, new CancellingDownloader(cts),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.InstallAsync(null, cts.Token));

        Assert.Equal("OLD", File.ReadAllText(Path.Combine(ffmpegDir, "ffmpeg.exe")));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task Install_keeps_existing_files_when_archive_lacks_an_executable()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "Data");
        var ffmpegDir = Path.Combine(data, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        File.WriteAllText(Path.Combine(ffmpegDir, "ffmpeg.exe"), "OLD");
        var archive = CreateArchiveWithOnlyFfmpeg();
        var manager = new FfmpegManager(data, new FakeDownloader(archive, Sha256(archive)),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InstallAsync(null, CancellationToken.None));

        Assert.Equal("OLD", File.ReadAllText(Path.Combine(ffmpegDir, "ffmpeg.exe")));
        Assert.Single(Directory.GetFileSystemEntries(data));
    }

    [Fact]
    public async Task ReadLocalVersions_run_the_local_executables()
    {
        using var temp = new TempDirectory();
        var executables = new List<string>();
        var runner = new FakeProcessRunner((executable, _, _) =>
        {
            executables.Add(executable);
            var output = executable.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
                ? "ffprobe version 9.0.2"
                : "ffmpeg version 9.0.2";
            return new ProcessResult(0, output, "");
        });
        var manager = new FfmpegManager(temp.Path, new FakeDownloader([], ""), runner);

        Assert.Equal("ffmpeg version 9.0.2", await manager.ReadLocalVersionAsync(CancellationToken.None));
        Assert.Equal("ffprobe version 9.0.2", await manager.ReadLocalProbeVersionAsync(CancellationToken.None));
        Assert.Equal([manager.FfmpegPath, manager.FfprobePath], executables);
    }

    [Fact]
    public async Task ReadLocalVersionAsync_returns_null_when_process_fails()
    {
        using var temp = new TempDirectory();
        var manager = new FfmpegManager(temp.Path, new FakeDownloader([], ""),
            new FakeProcessRunner(new ProcessResult(1, "ffmpeg version 9.0.2", "boom")));

        Assert.Null(await manager.ReadLocalVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetLatestVersionAsync_returns_the_trimmed_remote_version()
    {
        using var temp = new TempDirectory();
        var manager = new FfmpegManager(temp.Path, new VersionDownloader(" 9.0.3\r\n"),
            new FakeProcessRunner(new ProcessResult(0, "", "")));

        Assert.Equal("9.0.3", await manager.GetLatestVersionAsync(CancellationToken.None));
    }

    private static byte[] CreateArchive()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffmpeg.exe", "FFMPEG");
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffprobe.exe", "FFPROBE");
        }
        return ms.ToArray();
    }

    private static byte[] CreateArchiveWithOnlyFfmpeg()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffmpeg.exe", "FFMPEG");
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string Sha256(byte[] archive)
        => Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

    private sealed class FakeDownloader(byte[] archive, string checksum) : IFfmpegDownloader
    {
        public Task DownloadAsync(Uri uri, Stream destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
        {
            destination.Write(archive);
            progress?.Report(new DownloadProgress(archive.Length, archive.Length));
            return Task.CompletedTask;
        }

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct)
            => Task.FromResult(uri.AbsolutePath.EndsWith(".sha256") ? checksum : "9.0.2");
    }

    private sealed class ThrowingDownloader : IFfmpegDownloader
    {
        public Task DownloadAsync(Uri uri, Stream destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
            => throw new HttpRequestException("network down");

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct)
            => throw new HttpRequestException("network down");
    }

    private sealed class CancellingDownloader(CancellationTokenSource source) : IFfmpegDownloader
    {
        public Task DownloadAsync(Uri uri, Stream destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
        {
            destination.Write([1, 2, 3]);
            progress?.Report(new DownloadProgress(3, 100));
            source.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct)
            => Task.FromResult("9.0.2");
    }

    private sealed class VersionDownloader(string version) : IFfmpegDownloader
    {
        public Task DownloadAsync(Uri uri, Stream destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<string> GetStringAsync(Uri uri, CancellationToken ct)
            => Task.FromResult(version);
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

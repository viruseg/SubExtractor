using System.IO.Compression;
using System.Security.Cryptography;
using SubExtractor.Core.Ffmpeg;
using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Tests.TestHelpers;

public static class FfmpegTestData
{
    public static byte[] CreateArchive()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffmpeg.exe", "FFMPEG");
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffprobe.exe", "FFPROBE");
        }

        return stream.ToArray();
    }

    public static byte[] CreateArchiveWithOnlyFfmpeg()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "ffmpeg-9.0.2/bin/ffmpeg.exe", "FFMPEG");
        }

        return stream.ToArray();
    }

    public static string Sha256(byte[] archive) =>
        Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}

public sealed class FakeFfmpegDownloader(string version, byte[] archive, string checksum)
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

public sealed class ThrowingFfmpegDownloader : IFfmpegDownloader
{
    public Task DownloadAsync(
        Uri uri,
        Stream destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct) => throw new HttpRequestException("network down");

    public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
        throw new HttpRequestException("network down");
}

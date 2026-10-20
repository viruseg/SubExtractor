namespace SubExtractor.Core.Ffmpeg;

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percent => TotalBytes > 0
        ? (double)BytesReceived / TotalBytes.Value * 100
        : null;
}

using SubExtractor.Core.Models;

namespace SubExtractor.Core.Scanning;

public sealed record ScanReport(
    int FileCount,
    int TrackCount,
    string SummaryText,
    string? EmptyStateText,
    bool FolderFailed);

public static class ScanReportBuilder
{
    public static ScanReport Build(ScanResult result)
    {
        var fileCount = result.Videos.Count;
        var trackCount = result.Videos.Sum(video => video.Tracks.Count);
        var folderFailed = result.FolderError is not null;

        return new ScanReport(
            fileCount,
            trackCount,
            folderFailed
                ? "Scan finished: 0 files, 0 subtitle tracks"
                : $"Scan finished: {fileCount} files, {trackCount} subtitle tracks",
            folderFailed
                ? "Couldn't read this folder."
                : fileCount == 0
                    ? "No video files found in this folder."
                    : null,
            folderFailed);
    }

    public static string StartedText(string folder) => $"Scan started: {Path.GetFullPath(folder)}";
}

using SubExtractor.Core.Models;
using SubExtractor.Core.Scanning;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class ScanReportBuilderTests
{
    [Fact]
    public void Build_reports_a_folder_error()
    {
        var report = ScanReportBuilder.Build(new ScanResult([], "Access denied"));

        Assert.True(report.FolderFailed);
        Assert.Equal(0, report.FileCount);
        Assert.Equal(0, report.TrackCount);
        Assert.Equal("Scan finished: 0 files, 0 subtitle tracks", report.SummaryText);
        Assert.Equal("Couldn't read this folder.", report.EmptyStateText);
    }

    [Fact]
    public void Build_reports_an_empty_folder()
    {
        var report = ScanReportBuilder.Build(new ScanResult([], null));

        Assert.False(report.FolderFailed);
        Assert.Equal("Scan finished: 0 files, 0 subtitle tracks", report.SummaryText);
        Assert.Equal("No video files found in this folder.", report.EmptyStateText);
    }

    [Fact]
    public void Build_ignores_videos_when_the_folder_failed()
    {
        var report = ScanReportBuilder.Build(
            new ScanResult([Video("a.mkv", 2)], "Access denied"));

        Assert.True(report.FolderFailed);
        Assert.Equal("Scan finished: 0 files, 0 subtitle tracks", report.SummaryText);
        Assert.Equal("Couldn't read this folder.", report.EmptyStateText);
    }

    [Fact]
    public void Build_counts_files_and_tracks()
    {
        var report = ScanReportBuilder.Build(new ScanResult(
            [Video("a.mkv", 2), Video("b.mkv", 1)], null));

        Assert.Equal(2, report.FileCount);
        Assert.Equal(3, report.TrackCount);
        Assert.False(report.FolderFailed);
        Assert.Null(report.EmptyStateText);
        Assert.Equal("Scan finished: 2 files, 3 subtitle tracks", report.SummaryText);
    }

    [Fact]
    public void StartedText_uses_the_full_path()
    {
        using var temp = new TempDirectory();
        var folder = Path.Combine(temp.Path, "Videos");

        Assert.Equal($"Scan started: {folder}", ScanReportBuilder.StartedText(folder));
    }

    private static VideoItem Video(string name, int trackCount) =>
        new(name, ProbeStatus.Ok,
            Enumerable.Range(0, trackCount)
                .Select(index => new SubtitleTrack(name, index + 2, "subrip", null, "eng"))
                .ToList(),
            null);
}

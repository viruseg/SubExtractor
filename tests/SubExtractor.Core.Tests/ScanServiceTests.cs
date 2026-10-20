using SubExtractor.Core.Models;
using SubExtractor.Core.Scanning;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class ScanServiceTests
{
    [Fact]
    public async Task Scan_continues_after_one_file_fails()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "good.mkv"), "");
        File.WriteAllText(Path.Combine(temp.Path, "bad.mkv"), "");
        var probe = new FakeSubtitleProbe(path =>
            path.EndsWith("bad.mkv", StringComparison.OrdinalIgnoreCase)
                ? throw new InvalidOperationException("probe failed")
                : [new SubtitleTrack(path, 2, "subrip", "Full", "eng")]);
        var sink = new RecordingScanSink();

        var result = await new ScanService(probe).ScanAsync(temp.Path, sink, CancellationToken.None);

        Assert.Equal(2, result.Videos.Count);
        Assert.Equal(ProbeStatus.Ok, result.Videos.Single(v => v.FilePath.EndsWith("good.mkv")).Status);
        Assert.Equal(ProbeStatus.Failed, result.Videos.Single(v => v.FilePath.EndsWith("bad.mkv")).Status);
        Assert.Contains(sink.Errors, e => e.Contains("probe failed"));
        Assert.Equal([(1, 2)], sink.FailedProgress);
        Assert.Equal([(2, 2)], sink.FinishedProgress);
    }

    [Fact]
    public async Task Scan_returns_folder_error_for_missing_folder()
    {
        using var temp = new TempDirectory();
        var sink = new RecordingScanSink();
        var result = await new ScanService(new FakeSubtitleProbe(_ => []))
            .ScanAsync(Path.Combine(temp.Path, "missing"), sink, CancellationToken.None);

        Assert.Empty(result.Videos);
        Assert.NotNull(result.FolderError);
        Assert.Single(sink.FolderErrors);
    }

    [Fact]
    public async Task Scan_propagates_cancellation_without_partial_result()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a.mkv"), "");
        using var cts = new CancellationTokenSource();
        var probe = new FakeSubtitleProbe(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ScanService(probe).ScanAsync(temp.Path, new RecordingScanSink(), cts.Token));
    }

    private sealed class RecordingScanSink : IScanProgressSink
    {
        public List<string> Errors { get; } = [];
        public List<string> FolderErrors { get; } = [];
        public List<string> Started { get; } = [];
        public List<string> Finished { get; } = [];
        public List<(int Completed, int Total)> FinishedProgress { get; } = [];
        public List<(int Completed, int Total)> FailedProgress { get; } = [];

        public void FileStarted(string filePath, int completedFiles, int totalFiles)
            => Started.Add(filePath);
        public void FileFinished(string filePath, int completedFiles, int totalFiles, int trackCount)
        {
            Finished.Add(filePath);
            FinishedProgress.Add((completedFiles, totalFiles));
        }
        public void FileFailed(string filePath, int completedFiles, int totalFiles, string error)
        {
            Errors.Add(error);
            FailedProgress.Add((completedFiles, totalFiles));
        }
        public void FolderFailed(string error) => FolderErrors.Add(error);
    }
}

using SubExtractor.Core.Extraction;
using SubExtractor.Core.Models;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class ExtractionServiceTests
{
    [Fact]
    public async Task Extract_moves_temp_to_final_and_removes_temp()
    {
        using var temp = new TempDirectory();
        var video = Path.Combine(temp.Path, "Movie.mkv");
        var final = Path.Combine(temp.Path, "Movie.eng.srt");
        var plan = new OutputPlan(
            new SubtitleTrack(video, 2, "subrip", "Full", "eng"),
            final, ".srt", "srt", SubtitleWriteMode.Copy, false);
        var runner = new WritingProcessRunner("SUB");

        var summary = await new ExtractionService(runner, "ffmpeg.exe")
            .ExtractAsync([plan], new RecordingExtractionSink(), CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        Assert.Equal("SUB", File.ReadAllText(final));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        Assert.Contains("copy", runner.Calls[0]);
    }

    [Fact]
    public async Task Extract_overwrites_existing_file_after_success()
    {
        using var temp = new TempDirectory();
        var final = Path.Combine(temp.Path, "Movie.eng.srt");
        File.WriteAllText(final, "OLD");
        var plan = new OutputPlan(
            new SubtitleTrack(Path.Combine(temp.Path, "Movie.mkv"), 2, "subrip", null, "eng"),
            final, ".srt", "srt", SubtitleWriteMode.Copy, true);

        await new ExtractionService(new WritingProcessRunner("NEW"), "ffmpeg.exe")
            .ExtractAsync([plan], new RecordingExtractionSink(), CancellationToken.None);

        Assert.Equal("NEW", File.ReadAllText(final));
    }

    [Fact]
    public async Task Extract_failure_keeps_old_file_and_cleans_temp()
    {
        using var temp = new TempDirectory();
        var final = Path.Combine(temp.Path, "Movie.eng.srt");
        File.WriteAllText(final, "OLD");
        var plan = new OutputPlan(
            new SubtitleTrack(Path.Combine(temp.Path, "Movie.mkv"), 2, "subrip", null, "eng"),
            final, ".srt", "srt", SubtitleWriteMode.Copy, true);
        var sink = new RecordingExtractionSink();

        var summary = await new ExtractionService(new WritingProcessRunner("NEW", 1), "ffmpeg.exe")
            .ExtractAsync([plan], sink, CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal("OLD", File.ReadAllText(final));
        Assert.Single(sink.Failures);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Extract_uses_srt_encoding_for_mov_text()
    {
        using var temp = new TempDirectory();
        var plan = new OutputPlan(
            new SubtitleTrack(Path.Combine(temp.Path, "Movie.mp4"), 2, "mov_text", null, "eng"),
            Path.Combine(temp.Path, "Movie.eng.srt"), ".srt", "srt", SubtitleWriteMode.EncodeSrt, false);
        var runner = new WritingProcessRunner("SUB");

        await new ExtractionService(runner, "ffmpeg.exe")
            .ExtractAsync([plan], new RecordingExtractionSink(), CancellationToken.None);

        Assert.Contains("srt", runner.Calls[0]);
        Assert.DoesNotContain("copy", runner.Calls[0]);
    }

    [Fact]
    public async Task Extract_locked_target_is_a_failure_and_keeps_old_file()
    {
        using var temp = new TempDirectory();
        var final = Path.Combine(temp.Path, "Movie.eng.srt");
        File.WriteAllText(final, "OLD");
        var plan = new OutputPlan(
            new SubtitleTrack(Path.Combine(temp.Path, "Movie.mkv"), 2, "subrip", null, "eng"),
            final, ".srt", "srt", SubtitleWriteMode.Copy, true);
        var sink = new RecordingExtractionSink();

        using (var locked = new FileStream(final, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var summary = await new ExtractionService(new WritingProcessRunner("NEW"), "ffmpeg.exe")
                .ExtractAsync([plan], sink, CancellationToken.None);

            Assert.Equal(1, summary.Failed);
            Assert.Single(sink.Failures);
            Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        }

        Assert.Equal("OLD", File.ReadAllText(final));
    }

    [Fact]
    public async Task Extract_cancellation_keeps_completed_outputs_and_cleans_current_temp()
    {
        using var temp = new TempDirectory();
        var video = Path.Combine(temp.Path, "Movie.mkv");
        var firstFinal = Path.Combine(temp.Path, "Movie.eng.srt");
        var secondFinal = Path.Combine(temp.Path, "Movie.rus.srt");
        File.WriteAllText(secondFinal, "OLD");
        var plans = new[]
        {
            new OutputPlan(new SubtitleTrack(video, 2, "subrip", null, "eng"),
                firstFinal, ".srt", "srt", SubtitleWriteMode.Copy, false),
            new OutputPlan(new SubtitleTrack(video, 3, "subrip", null, "rus"),
                secondFinal, ".srt", "srt", SubtitleWriteMode.Copy, true),
        };
        var runner = new SucceedThenCancelRunner();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ExtractionService(runner, "ffmpeg.exe")
                .ExtractAsync(plans, new RecordingExtractionSink(), CancellationToken.None));

        Assert.Equal("OK", File.ReadAllText(firstFinal));
        Assert.Equal("OLD", File.ReadAllText(secondFinal));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Extract_reports_per_file_and_overall_progress()
    {
        using var temp = new TempDirectory();
        var video = Path.Combine(temp.Path, "Movie.mkv");
        var plans = new[]
        {
            new OutputPlan(new SubtitleTrack(video, 2, "subrip", null, "eng"),
                Path.Combine(temp.Path, "Movie.eng.srt"), ".srt", "srt", SubtitleWriteMode.Copy, false),
            new OutputPlan(new SubtitleTrack(video, 3, "subrip", null, "rus"),
                Path.Combine(temp.Path, "Movie.rus.srt"), ".srt", "srt", SubtitleWriteMode.Copy, false),
        };
        var sink = new RecordingExtractionSink();

        await new ExtractionService(new WritingProcessRunner("SUB"), "ffmpeg.exe")
            .ExtractAsync(plans, sink, CancellationToken.None);

        Assert.Equal([(1, 2, 1, 2), (2, 2, 2, 2)], sink.TrackProgress);
    }

    [Fact]
    public async Task Extract_continues_with_next_file_after_a_failure()
    {
        using var temp = new TempDirectory();
        var firstVideo = Path.Combine(temp.Path, "First.mkv");
        var secondVideo = Path.Combine(temp.Path, "Second.mkv");
        var firstFinal = Path.Combine(temp.Path, "First.eng.srt");
        var secondFinal = Path.Combine(temp.Path, "Second.rus.srt");
        var plans = new[]
        {
            new OutputPlan(new SubtitleTrack(firstVideo, 2, "subrip", null, "eng"),
                firstFinal, ".srt", "srt", SubtitleWriteMode.Copy, false),
            new OutputPlan(new SubtitleTrack(secondVideo, 3, "subrip", null, "rus"),
                secondFinal, ".srt", "srt", SubtitleWriteMode.Copy, false),
        };
        var sink = new RecordingExtractionSink();

        var summary = await new ExtractionService(new FailThenSucceedRunner(), "ffmpeg.exe")
            .ExtractAsync(plans, sink, CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.False(File.Exists(firstFinal));
        Assert.Equal("OK", File.ReadAllText(secondFinal));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        Assert.Contains("boom", Assert.Single(sink.Failures));
        Assert.Equal(
            [
                "FileStarted:First.mkv:0/2",
                "TrackFailed:First.mkv:1/1:1/2",
                "FileFinished:First.mkv",
                "FileStarted:Second.mkv:1/2",
                "TrackFinished:Second.mkv:1/1:2/2",
                "FileFinished:Second.mkv",
            ],
            sink.Events);
    }

    private sealed class WritingProcessRunner(string content, int exitCode = 0) : IProcessRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            Calls.Add(arguments);
            File.WriteAllText(arguments[^1], content);
            return Task.FromResult(new ProcessResult(exitCode, "", exitCode == 0 ? "" : "boom"));
        }
    }

    private sealed class SucceedThenCancelRunner : IProcessRunner
    {
        private int _calls;

        public Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            _calls++;
            File.WriteAllText(arguments[^1], _calls == 1 ? "OK" : "PARTIAL");
            if (_calls == 2)
                throw new OperationCanceledException();
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class FailThenSucceedRunner : IProcessRunner
    {
        private int _calls;

        public Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            _calls++;
            if (_calls == 1)
                return Task.FromResult(new ProcessResult(1, "", "boom"));

            File.WriteAllText(arguments[^1], "OK");
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class RecordingExtractionSink : IExtractionProgressSink
    {
        public List<string> Failures { get; } = [];
        public List<(int InFile, int InFileTotal, int Overall, int OverallTotal)> TrackProgress { get; } = [];
        public List<string> Events { get; } = [];

        public void FileStarted(string videoPath, int completedOverall, int totalOverall)
            => Events.Add($"FileStarted:{Path.GetFileName(videoPath)}:{completedOverall}/{totalOverall}");

        public void TrackFinished(string videoPath, int completedInFile, int totalInFile,
            int completedOverall, int totalOverall)
        {
            TrackProgress.Add((completedInFile, totalInFile, completedOverall, totalOverall));
            Events.Add(
                $"TrackFinished:{Path.GetFileName(videoPath)}:{completedInFile}/{totalInFile}:{completedOverall}/{totalOverall}");
        }

        public void TrackFailed(string videoPath, int completedInFile, int totalInFile,
            int completedOverall, int totalOverall, string error)
        {
            Failures.Add(error);
            Events.Add(
                $"TrackFailed:{Path.GetFileName(videoPath)}:{completedInFile}/{totalInFile}:{completedOverall}/{totalOverall}");
        }

        public void FileFinished(string videoPath)
            => Events.Add($"FileFinished:{Path.GetFileName(videoPath)}");
    }
}

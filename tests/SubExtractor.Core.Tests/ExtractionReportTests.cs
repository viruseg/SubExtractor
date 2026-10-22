using SubExtractor.Core.Extraction;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests;

public sealed class ExtractionReportTests
{
    [Fact]
    public void HasOverwrites_detects_any_existing_target()
    {
        Assert.True(ExtractionReport.HasOverwrites([
            Plan("Movie.eng.srt", false),
            Plan("Movie.rus.srt", true),
        ]));
        Assert.False(ExtractionReport.HasOverwrites([Plan("Movie.eng.srt", false)]));
        Assert.False(ExtractionReport.HasOverwrites([]));
    }

    [Fact]
    public void RecycleFailureMessages_preserve_stage_hresult_and_attempts()
    {
        var failure = new FileRecycleFailure(
            @"C:\Videos\Movie.eng.srt", "Performing", unchecked((int)0x80070005), 3);

        var message = Assert.Single(ExtractionReport.RecycleFailureMessages([failure]));

        Assert.Equal(
            "Could not move 'Movie.eng.srt' to the Recycle Bin: Performing (0x80070005) after 3 attempt(s). This subtitle will be skipped.",
            message);
    }

    [Theory]
    [InlineData(true, true, "Time shift and text removal are")]
    [InlineData(true, false, "Time shift is")]
    [InlineData(false, true, "Text removal is")]
    public void SrtOnlySkipMessages_describe_the_applied_operations(
        bool appliesTimeShift,
        bool appliesTextRemoval,
        string operations)
    {
        var plans = new[]
        {
            Plan("Movie.eng.srt", false),
            Plan("Movie.eng.SRT", false),
            Plan("Movie.eng.ass", false),
        };

        var message = Assert.Single(
            ExtractionReport.SrtOnlySkipMessages(plans, appliesTimeShift, appliesTextRemoval));

        Assert.Equal(
            $"{operations} only applied to SRT subtitles; skipped 'Movie.eng.ass'.", message);
    }

    [Fact]
    public void SrtOnlySkipMessages_is_empty_when_no_operation_applies()
    {
        Assert.Empty(ExtractionReport.SrtOnlySkipMessages(
            [Plan("Movie.eng.ass", false)], false, false));
    }

    [Fact]
    public void SummaryText_reports_succeeded_and_failed_counts()
    {
        Assert.Equal(
            "Extraction finished: 3 succeeded, 2 failed.",
            ExtractionReport.SummaryText(new ExtractionSummary(3, 2)));
    }

    private static OutputPlan Plan(string fileName, bool overwritesExisting) =>
        new(
            new SubtitleTrack(Path.Combine("C:\\", "Videos", "Movie.mkv"), 2, "subrip", null, "eng"),
            Path.Combine("C:\\", "Videos", fileName),
            Path.GetExtension(fileName),
            Path.GetExtension(fileName).TrimStart('.'),
            SubtitleWriteMode.Copy,
            overwritesExisting);
}

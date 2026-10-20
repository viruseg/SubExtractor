using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests;

public class OverwritePromptTests
{
    [Fact]
    public void BuildLines_lists_only_existing_targets_in_plan_order()
    {
        var plans = new[]
        {
            Plan("Movie.eng.srt", overwritesExisting: false),
            Plan("Movie.rus.srt", overwritesExisting: true),
            Plan("Movie.jpn.srt", overwritesExisting: true),
        };

        Assert.Equal(["Movie.rus.srt", "Movie.jpn.srt"], OverwritePrompt.BuildLines(plans));
    }

    [Fact]
    public void BuildLines_is_empty_when_no_target_exists()
    {
        var plans = new[] { Plan("Movie.eng.srt", overwritesExisting: false) };

        Assert.Empty(OverwritePrompt.BuildLines(plans));
    }

    [Theory]
    [InlineData(10, 10, "Movie.10.srt")]
    [InlineData(11, 11, "and 1 more")]
    [InlineData(12, 11, "and 2 more")]
    public void BuildLines_limits_named_files_to_ten(int planCount, int lineCount, string lastLine)
    {
        var plans = Enumerable.Range(1, planCount)
            .Select(index => Plan($"Movie.{index:00}.srt", overwritesExisting: true))
            .ToList();

        var lines = OverwritePrompt.BuildLines(plans);

        Assert.Equal(lineCount, lines.Count);
        Assert.Equal("Movie.01.srt", lines[0]);
        Assert.Equal("Movie.10.srt", lines[9]);
        Assert.Equal(lastLine, lines[^1]);
    }

    private static OutputPlan Plan(string fileName, bool overwritesExisting) =>
        new(
            new SubtitleTrack(
                Path.Combine("C:\\", "Videos", "Movie.mkv"), 2, "subrip", null, "eng"),
            Path.Combine("C:\\", "Videos", fileName),
            ".srt",
            "srt",
            SubtitleWriteMode.Copy,
            overwritesExisting);
}

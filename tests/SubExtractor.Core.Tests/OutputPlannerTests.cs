using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests;

public class OutputPlannerTests
{
    [Fact]
    public void Plan_uses_language_suffix_and_marks_existing_targets()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var track = new SubtitleTrack(video, 2, "subrip", "Full", "eng");
        var expected = Path.Combine("C:\\", "Videos", "Movie.eng.srt");

        var plan = Assert.Single(OutputPlanner.Plan(
            Path.Combine("C:\\", "Videos"), [track], path => path == expected));

        Assert.Equal(expected, plan.FinalPath);
        Assert.True(plan.OverwritesExisting);
        Assert.Equal(SubtitleWriteMode.Copy, plan.Mode);
    }

    [Fact]
    public void Plan_adds_stream_index_for_duplicate_name_and_language()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var tracks = new[]
        {
            new SubtitleTrack(video, 2, "subrip", "Full", "eng"),
            new SubtitleTrack(video, 3, "subrip", "Full", "eng"),
        };

        var names = OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), tracks, _ => false)
            .Select(p => Path.GetFileName(p.FinalPath))
            .ToArray();

        Assert.Equal(["Movie.eng.srt", "Movie.eng.3.srt"], names);
    }

    [Fact]
    public void Plan_keeps_cross_video_paths_unique()
    {
        var folder = Path.Combine("C:\\", "Videos");
        var tracks = new[]
        {
            new SubtitleTrack(Path.Combine(folder, "Movie.mkv"), 2, "subrip", null, "eng"),
            new SubtitleTrack(Path.Combine(folder, "Movie.mp4"), 2, "subrip", null, "eng"),
            new SubtitleTrack(Path.Combine(folder, "Movie.avi"), 2, "subrip", null, "eng"),
        };

        var names = OutputPlanner.Plan(folder, tracks, _ => false)
            .Select(p => Path.GetFileName(p.FinalPath))
            .ToArray();

        Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("Movie.eng.srt", names);
        Assert.Contains("Movie.eng.2.srt", names);
        Assert.Contains("Movie.eng.2-2.srt", names);
    }

    [Fact]
    public void Plan_does_not_add_index_when_extensions_differ()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var tracks = new[]
        {
            new SubtitleTrack(video, 2, "subrip", "Full", "eng"),
            new SubtitleTrack(video, 3, "ass", "Full", "eng"),
        };

        var names = OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), tracks, _ => false)
            .Select(p => Path.GetFileName(p.FinalPath))
            .ToArray();

        Assert.Equal(["Movie.eng.srt", "Movie.eng.ass"], names);
    }

    [Fact]
    public void Plan_sanitizes_language_for_file_names()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var track = new SubtitleTrack(video, 2, "subrip", null, "e?g");
        var plan = Assert.Single(OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), [track], _ => false));
        Assert.Equal("Movie.e_g.srt", Path.GetFileName(plan.FinalPath));
    }

    [Fact]
    public void Plan_preserves_the_raw_language_tag_case()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var track = new SubtitleTrack(video, 2, "subrip", "Full", "ENG");
        var plan = Assert.Single(OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), [track], _ => false));
        Assert.Equal("Movie.ENG.srt", Path.GetFileName(plan.FinalPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Plan_falls_back_to_und_for_missing_language(string? language)
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var track = new SubtitleTrack(video, 2, "subrip", null, language);
        var plan = Assert.Single(OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), [track], _ => false));
        Assert.Equal("Movie.und.srt", Path.GetFileName(plan.FinalPath));
    }

    [Fact]
    public void Plan_treats_claimed_paths_case_insensitively()
    {
        var video = Path.Combine("C:\\", "Videos", "Movie.mkv");
        var tracks = new[]
        {
            new SubtitleTrack(video, 2, "subrip", null, "eng"),
            new SubtitleTrack(video, 3, "subrip", null, "ENG"),
        };

        var names = OutputPlanner.Plan(Path.Combine("C:\\", "Videos"), tracks, _ => false)
            .Select(p => Path.GetFileName(p.FinalPath))
            .ToArray();

        Assert.Equal(["Movie.eng.srt", "Movie.ENG.3.srt"], names);
    }
}

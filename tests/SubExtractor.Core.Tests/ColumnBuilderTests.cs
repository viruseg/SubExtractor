using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests;

public class ColumnBuilderTests
{
    private static VideoItem Video(string path, params (string? Title, string? Language)[] tracks) =>
        new(path, ProbeStatus.Ok,
            tracks.Select((t, i) => new SubtitleTrack(path, i + 2, "subrip", t.Title, t.Language)).ToList(),
            null);

    [Fact]
    public void Build_collapses_same_pair_across_files_and_keeps_first_spelling()
    {
        var columns = ColumnBuilder.Build([
            Video("a.mkv", ("English", "eng")),
            Video("b.mkv", ("english", "ENG")),
        ]);

        var column = Assert.Single(columns);
        Assert.Equal("English", column.DisplayTitle);
        Assert.Equal("eng", column.DisplayLanguage);
        Assert.Equal("English (eng)", column.HeaderLabel);
    }

    [Fact]
    public void Build_orders_by_language_then_title()
    {
        var columns = ColumnBuilder.Build([
            Video("a.mkv", ("SDH", "eng"), ("Full", "rus"), ("Full", "eng")),
        ]);

        Assert.Equal(["Full (eng)", "SDH (eng)", "Full (rus)"], columns.Select(c => c.HeaderLabel));
    }

    [Fact]
    public void Build_treats_missing_language_and_literal_und_as_one_column()
    {
        var columns = ColumnBuilder.Build([
            Video("a.mkv", ("Full", null)),
            Video("b.mkv", ("full", "und")),
        ]);

        var column = Assert.Single(columns);
        Assert.Equal("Full (und)", column.HeaderLabel);
    }
}

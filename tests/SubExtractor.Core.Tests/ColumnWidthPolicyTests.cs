using SubExtractor.Core.Formatting;

namespace SubExtractor.Core.Tests;

public class ColumnWidthPolicyTests
{
    [Fact]
    public void Measure_returns_min_width_when_both_labels_are_empty()
        => Assert.Equal(ColumnWidthPolicy.MinWidth, ColumnWidthPolicy.Measure("", ""));

    [Fact]
    public void Measure_clamps_a_short_title_to_min_width()
        => Assert.Equal(ColumnWidthPolicy.MinWidth, ColumnWidthPolicy.Measure("English", "eng"));

    [Fact]
    public void Measure_clamps_a_long_title_to_max_width()
        => Assert.Equal(ColumnWidthPolicy.MaxWidth, ColumnWidthPolicy.Measure(new string('W', 40), "eng"));

    [Fact]
    public void Measure_fits_a_typical_title()
        => Assert.Equal(129.19, ColumnWidthPolicy.Measure("English (forced)", "eng"), 2);

    [Fact]
    public void Measure_falls_back_to_the_language_when_the_title_is_empty()
        => Assert.True(
            ColumnWidthPolicy.Measure("", "ABCDEFGHIJKLMNOPQRST") > ColumnWidthPolicy.MinWidth);

    [Fact]
    public void Measure_ignores_the_case_of_the_language()
        => Assert.Equal(
            ColumnWidthPolicy.Measure("x", "eng"),
            ColumnWidthPolicy.Measure("x", "ENG"));

    [Fact]
    public void Measure_never_leaves_the_allowed_range()
    {
        foreach (var title in new[] { "", "A", new string('W', 200), "Русский (forced)" })
        {
            foreach (var language in new[] { "", "eng", "und" })
            {
                var width = ColumnWidthPolicy.Measure(title, language);
                Assert.InRange(width, ColumnWidthPolicy.MinWidth, ColumnWidthPolicy.MaxWidth);
            }
        }
    }

    [Fact]
    public void Measure_never_shrinks_as_the_title_grows()
    {
        var previous = 0d;
        for (var length = 0; length <= 40; length++)
        {
            var width = ColumnWidthPolicy.Measure(new string('W', length), "eng");
            Assert.True(width >= previous, $"width shrank at title length {length}");
            previous = width;
        }
    }

    [Fact]
    public void MeasureFileCell_fits_the_file_name()
        => Assert.Equal(89.0, ColumnWidthPolicy.MeasureFileCell("movie.mkv", string.Empty), 2);

    [Fact]
    public void MeasureFileCell_adds_the_status_next_to_the_name()
        => Assert.Equal(165.4, ColumnWidthPolicy.MeasureFileCell("movie.mkv", "No subtitles"), 2);

    [Fact]
    public void MeasureFileCell_never_shrinks_as_the_name_grows()
    {
        var previous = 0d;
        for (var length = 0; length <= 40; length++)
        {
            var width = ColumnWidthPolicy.MeasureFileCell(new string('W', length), string.Empty);
            Assert.True(width >= previous, $"width shrank at name length {length}");
            previous = width;
        }
    }

    [Fact]
    public void MeasureFileCell_ignores_a_missing_status()
        => Assert.Equal(
            ColumnWidthPolicy.MeasureFileCell("movie.mkv", string.Empty),
            ColumnWidthPolicy.MeasureFileCell("movie.mkv", null));
}

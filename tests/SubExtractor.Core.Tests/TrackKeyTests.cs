using SubExtractor.Core.Formatting;

namespace SubExtractor.Core.Tests;

public class TrackKeyTests
{
    [Theory]
    [InlineData("English", "eng", "english\u001feng")]
    [InlineData("  English ", " ENG ", "english\u001feng")]
    [InlineData("", "und", "\u001fund")]
    [InlineData(null, null, "\u001fund")]
    [InlineData("SDH", "eng", "sdh\u001feng")]
    public void Build_normalizes_title_and_language(string? title, string? language, string expected)
        => Assert.Equal(expected, TrackKey.Build(title, language));
}

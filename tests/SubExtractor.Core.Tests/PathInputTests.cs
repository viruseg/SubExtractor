using SubExtractor.Core.Formatting;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class PathInputTests
{
    [Theory]
    [InlineData("  C:\\Videos  ", "C:\\Videos")]
    [InlineData("\"C:\\Videos\"", "C:\\Videos")]
    [InlineData("  \"C:\\My Videos\"  ", "C:\\My Videos")]
    [InlineData("C:\\My Videos", "C:\\My Videos")]
    [InlineData("'C:\\Videos'", "C:\\Videos")]
    [InlineData("\"C:\\Videos", "\"C:\\Videos")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Sanitize_trims_and_removes_matching_quotes(string? input, string expected)
        => Assert.Equal(expected, PathInput.Sanitize(input));

    [Fact]
    public void IsExistingFolder_checks_the_sanitized_path()
    {
        using var temp = new TempDirectory();
        Assert.True(PathInput.IsExistingFolder($"  \"{temp.Path}\"  "));
        Assert.False(PathInput.IsExistingFolder(Path.Combine(temp.Path, "missing")));
    }
}

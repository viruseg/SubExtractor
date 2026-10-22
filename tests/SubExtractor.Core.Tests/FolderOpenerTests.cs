using SubExtractor.Core.Shell;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class FolderOpenerTests
{
    [Fact]
    public void NormalizePath_removes_a_trailing_separator()
    {
        using var temp = new TempDirectory();
        var expected = Path.Combine(temp.Path, "Videos");

        Assert.Equal(expected, FolderOpener.NormalizePath(expected + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void NormalizePath_returns_a_fully_qualified_path()
    {
        var normalized = FolderOpener.NormalizePath(".");

        Assert.True(Path.IsPathFullyQualified(normalized));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\0")]
    public void Open_returns_an_error_for_an_invalid_path(string path)
    {
        var result = new FolderOpener().Open(path);

        Assert.False(result.Opened);
        Assert.StartsWith("Couldn't open the folder:", result.ErrorMessage);
    }
}

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

    [Fact]
    public void Open_invokes_the_launcher_with_the_normalized_path()
    {
        using var temp = new TempDirectory();
        string? launched = null;
        var opener = new FolderOpener(path => launched = path);

        var result = opener.Open(temp.Path + Path.DirectorySeparatorChar);

        Assert.True(result.Opened);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(Path.TrimEndingDirectorySeparator(temp.Path), launched);
    }

    [Fact]
    public void Open_returns_an_error_when_the_launcher_throws()
    {
        using var temp = new TempDirectory();
        var opener = new FolderOpener(_ => throw new InvalidOperationException("boom"));

        var result = opener.Open(temp.Path);

        Assert.False(result.Opened);
        Assert.Equal("Couldn't open the folder: boom", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_rejects_a_null_launcher()
    {
        Assert.Throws<ArgumentNullException>(() => new FolderOpener(null!));
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

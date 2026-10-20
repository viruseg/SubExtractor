using SubExtractor.Core.Ffmpeg;

namespace SubExtractor.Core.Tests;

public sealed class FfmpegVersionTests
{
    [Theory]
    [InlineData("ffmpeg version 9.0.2-full_build-www.gyan.dev Copyright", "9.0.2", "9.0.2")]
    [InlineData("ffmpeg version 8.1.1-full_build", "8.1.1", "8.1.1")]
    [InlineData("ffmpeg version N-123-gabc", "N-123-gabc", null)]
    [InlineData("not ffmpeg", "", null)]
    public void Parse_extracts_numeric_prefix(string output, string display, string? numeric)
    {
        var parsed = FfmpegVersionParser.Parse(output);
        Assert.Equal(display, parsed.Display);
        Assert.Equal(numeric is null ? null : Version.Parse(numeric), parsed.Numeric);
    }

    [Theory]
    [InlineData("9.0.2-full_build", "9.0.3", true)]
    [InlineData("9.0.2-full_build", "9.0.2", false)]
    [InlineData("9.0.2-full_build", "8.1.1", false)]
    [InlineData("garbage", "9.0.3", false)]
    public void IsNewer_compares_numeric_versions(string local, string remote, bool expected)
        => Assert.Equal(expected, VersionComparison.IsNewer(local, remote));

    [Theory]
    [InlineData("9.0.2-full_build", "9.0.2")]
    [InlineData("9.0.3", "9.0.3")]
    [InlineData("garbage", null)]
    public void ParseNumeric_extracts_the_first_version(string input, string? expected)
        => Assert.Equal(expected is null ? null : Version.Parse(expected), VersionComparison.ParseNumeric(input));
}

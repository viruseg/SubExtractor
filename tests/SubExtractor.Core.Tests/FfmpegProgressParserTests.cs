using SubExtractor.Core.Extraction;

namespace SubExtractor.Core.Tests;

public class FfmpegProgressParserTests
{
    [Theory]
    [InlineData("out_time_us=577577000", 577.577)]
    [InlineData("out_time_ms=1246787000", 1246.787)]
    [InlineData("out_time=00:09:37.577000", 577.577)]
    [InlineData(" out_time_us = 1500000 ", 1.5)]
    public void TryParsePosition_reads_supported_progress_keys(string line, double seconds)
    {
        Assert.True(FfmpegProgressParser.TryParsePosition(line, out var position));
        Assert.Equal(seconds, position.TotalSeconds, 3);
    }

    [Theory]
    [InlineData("progress=continue")]
    [InlineData("out_time_us=N/A")]
    [InlineData("out_time_us=-1")]
    [InlineData("out_time_us=")]
    [InlineData("out_time_us=9223372036854775807")]
    [InlineData("out_time=not-a-time")]
    [InlineData("garbage")]
    public void TryParsePosition_ignores_unusable_lines(string line)
        => Assert.False(FfmpegProgressParser.TryParsePosition(line, out _));
}

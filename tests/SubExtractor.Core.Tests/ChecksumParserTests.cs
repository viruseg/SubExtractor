using SubExtractor.Core.Ffmpeg;

namespace SubExtractor.Core.Tests;

public sealed class ChecksumParserTests
{
    [Theory]
    [InlineData("60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba", "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba")]
    [InlineData("  60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA  ", "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba")]
    [InlineData("60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba  file.zip", "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba")]
    [InlineData("not-a-checksum", null)]
    [InlineData("", null)]
    public void Parse_reads_the_first_sha256(string content, string? expected)
        => Assert.Equal(expected, ChecksumParser.Parse(content));
}

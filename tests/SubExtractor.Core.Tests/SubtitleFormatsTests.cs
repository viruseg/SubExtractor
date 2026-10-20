using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests;

public class SubtitleFormatsTests
{
    [Theory]
    [InlineData("subrip", ".srt", "srt", SubtitleWriteMode.Copy)]
    [InlineData("ass", ".ass", "ass", SubtitleWriteMode.Copy)]
    [InlineData("ssa", ".ass", "ass", SubtitleWriteMode.Copy)]
    [InlineData("webvtt", ".vtt", "webvtt", SubtitleWriteMode.Copy)]
    [InlineData("mov_text", ".srt", "srt", SubtitleWriteMode.EncodeSrt)]
    [InlineData("text", ".srt", "srt", SubtitleWriteMode.EncodeSrt)]
    [InlineData("hdmv_pgs_subtitle", ".sup", "sup", SubtitleWriteMode.Copy)]
    [InlineData("dvd_subtitle", ".mks", "matroska", SubtitleWriteMode.Copy)]
    [InlineData("weird_codec", ".srt", "srt", SubtitleWriteMode.EncodeSrt)]
    public void For_maps_codec_to_output(string codec, string extension, string format, SubtitleWriteMode mode)
    {
        var result = SubtitleFormats.For(codec);
        Assert.Equal(extension, result.Extension);
        Assert.Equal(format, result.FfmpegFormat);
        Assert.Equal(mode, result.Mode);
    }

    [Fact]
    public void For_is_case_insensitive_and_defaults_null_to_encode()
    {
        Assert.Equal(new SubtitleFormat(".srt", "srt", SubtitleWriteMode.Copy), SubtitleFormats.For(" SUBRIP "));
        Assert.Equal(new SubtitleFormat(".srt", "srt", SubtitleWriteMode.EncodeSrt), SubtitleFormats.For(null));
    }
}

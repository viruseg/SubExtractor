using System.Text.Json;
using SubExtractor.Core.Ffprobe;

namespace SubExtractor.Core.Tests;

public class FfprobeJsonParserTests
{
    [Fact]
    public void Parse_reads_index_codec_title_and_language()
    {
        const string json = """
        {"streams":[
          {"index":2,"codec_name":"subrip","tags":{"language":"eng","title":"Full"}},
          {"index":3,"codec_name":"ass","tags":{"language":"rus"}},
          {"index":4,"codec_name":"subrip"}
        ]}
        """;

        var tracks = FfprobeJsonParser.Parse(json, @"C:\v\Movie.mkv");

        Assert.Equal(3, tracks.Count);
        Assert.Equal(2, tracks[0].StreamIndex);
        Assert.Equal("subrip", tracks[0].CodecName);
        Assert.Equal("Full", tracks[0].Title);
        Assert.Equal("eng", tracks[0].Language);
        Assert.Null(tracks[1].Title);
        Assert.Null(tracks[2].Title);
        Assert.Null(tracks[2].Language);
    }

    [Theory]
    [InlineData("""{"streams":[]}""")]
    [InlineData("{}")]
    public void Parse_returns_empty_when_no_subtitle_streams(string json)
        => Assert.Empty(FfprobeJsonParser.Parse(json, "x.mkv"));

    [Fact]
    public void Parse_throws_on_malformed_json()
        => Assert.ThrowsAny<JsonException>(() => FfprobeJsonParser.Parse("{", "x.mkv"));

    [Fact]
    public void Parse_reads_video_duration_from_format()
    {
        const string json = """
        {"streams":[{"index":2,"codec_name":"subrip"}],"format":{"duration":"2914.720000"}}
        """;

        var track = Assert.Single(FfprobeJsonParser.Parse(json, "x.mkv"));

        Assert.Equal(TimeSpan.FromSeconds(2914.72), track.VideoDuration);
    }

    [Theory]
    [InlineData("""{"streams":[{"index":2}]}""")]
    [InlineData("""{"streams":[{"index":2}],"format":{}}""")]
    [InlineData("""{"streams":[{"index":2}],"format":{"duration":"N/A"}}""")]
    [InlineData("""{"streams":[{"index":2}],"format":{"duration":"0"}}""")]
    [InlineData("""{"streams":[{"index":2}],"format":{"duration":"-5"}}""")]
    [InlineData("""{"streams":[{"index":2}],"format":{"duration":"abc"}}""")]
    public void Parse_leaves_duration_null_when_format_duration_is_unusable(string json)
        => Assert.Null(Assert.Single(FfprobeJsonParser.Parse(json, "x.mkv")).VideoDuration);
}

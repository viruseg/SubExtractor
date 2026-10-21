using System.Text;
using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class SrtTimeShifterTests
{
    [Fact]
    public void Shift_adds_offset_to_start_and_end()
    {
        var content = "1\n00:00:05,052 --> 00:00:06,251\nSubtitle text\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromMilliseconds(1500));

        Assert.Equal("1\n00:00:06,552 --> 00:00:07,751\nSubtitle text\n", result);
    }

    [Fact]
    public void Shift_subtracts_offset_from_start_and_end()
    {
        var content = "1\n00:00:05,052 --> 00:00:06,251\nSubtitle text\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromSeconds(-2));

        Assert.Equal("1\n00:00:03,052 --> 00:00:04,251\nSubtitle text\n", result);
    }

    [Theory]
    [InlineData("00:00:59,900", 200, "00:01:00,100")]
    [InlineData("00:59:59,900", 200, "01:00:00,100")]
    [InlineData("99:59:59,999", 1, "100:00:00,000")]
    public void Shift_carries_over_into_higher_units(string start, int offsetMilliseconds, string expected)
    {
        var content = $"1\n{start} --> {start}\nText\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromMilliseconds(offsetMilliseconds));

        Assert.Equal($"1\n{expected} --> {expected}\nText\n", result);
    }

    [Fact]
    public void Shift_writes_negative_timestamps_without_clamping()
    {
        var content = "1\n00:00:01,000 --> 00:00:01,600\nText\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromMilliseconds(-2500));

        Assert.Equal("1\n-00:00:01,500 --> -00:00:00,900\nText\n", result);
    }

    [Fact]
    public void Shift_preserves_dot_separator()
    {
        var content = "1\n00:00:01.000 --> 00:00:02.000\nText\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromMilliseconds(500));

        Assert.Equal("1\n00:00:01.500 --> 00:00:02.500\nText\n", result);
    }

    [Fact]
    public void Shift_preserves_text_indices_and_crlf()
    {
        var content =
            "1\r\n00:00:01,000 --> 00:00:02,000\r\nПервый\r\n\r\n" +
            "2\r\n00:00:03,000 --> 00:00:04,000\r\nSecond --> line\r\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromMilliseconds(250));

        Assert.Equal(
            "1\r\n00:00:01,250 --> 00:00:02,250\r\nПервый\r\n\r\n" +
            "2\r\n00:00:03,250 --> 00:00:04,250\r\nSecond --> line\r\n",
            result);
    }

    [Fact]
    public void Shift_does_not_change_text_lines_that_contain_an_arrow_and_timestamp()
    {
        var content =
            "1\n00:00:01,000 --> 00:00:02,000\nSee 00:00:01,000 --> 00:00:02,000\n";

        var result = SrtTimeShifter.Shift(content, TimeSpan.FromSeconds(1));

        Assert.Equal(
            "1\n00:00:02,000 --> 00:00:03,000\nSee 00:00:01,000 --> 00:00:02,000\n",
            result);
    }

    [Fact]
    public void Shift_with_zero_offset_returns_content_unchanged()
    {
        var content = "1\n00:00:01,000 --> 00:00:02,000\nText\n";

        Assert.Equal(content, SrtTimeShifter.Shift(content, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShiftFile_preserves_utf8_bom(bool withBom)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var content = "1\r\n00:00:05,052 --> 00:00:06,251\r\nПривет\r\n";
        var preamble = withBom ? Encoding.UTF8.GetPreamble() : [];
        File.WriteAllBytes(path, [.. preamble, .. Encoding.UTF8.GetBytes(content)]);

        SrtTimeShifter.ShiftFile(path, TimeSpan.FromSeconds(1));

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(preamble, bytes[..preamble.Length]);
        Assert.Equal(
            "1\r\n00:00:06,052 --> 00:00:07,251\r\nПривет\r\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void ShiftFile_with_zero_offset_leaves_file_bytes_untouched()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var bytes = Encoding.UTF8.GetBytes("1\n00:00:05,052 --> 00:00:06,251\nText\n");
        File.WriteAllBytes(path, bytes);

        SrtTimeShifter.ShiftFile(path, TimeSpan.Zero);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
}

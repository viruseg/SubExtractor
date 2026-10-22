using SubExtractor.Core.Extraction;

namespace SubExtractor.Core.Tests;

public sealed class TimeShiftTextTests
{
    [Fact]
    public void Format_zero_uses_positive_zero_fields()
    {
        Assert.Equal(
            new TimeShiftFields(false, "00", "00", "00", "000"),
            TimeShiftText.Format(TimeSpan.Zero));
    }

    [Fact]
    public void Format_negative_preserves_the_sign_and_absolute_parts()
    {
        Assert.Equal(
            new TimeShiftFields(true, "01", "02", "03", "004"),
            TimeShiftText.Format(TimeSpan.FromMilliseconds(-3_723_004)));
    }

    [Fact]
    public void Format_supports_two_digit_hours()
    {
        var fields = TimeShiftText.Format(TimeSpan.FromHours(99));

        Assert.Equal("99", fields.Hours);
        Assert.False(fields.IsNegative);
    }

    [Fact]
    public void Format_clamps_hours_to_the_parser_limit()
    {
        var value = TimeSpan.FromHours(100) + TimeSpan.FromMinutes(30);
        var fields = TimeShiftText.Format(value);

        Assert.Equal("99", fields.Hours);
        Assert.Equal("30", fields.Minutes);
        Assert.Equal(TimeSpan.FromHours(99) + TimeSpan.FromMinutes(30), TimeShiftText.Parse(fields));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3_723_004)]
    [InlineData(-3_723_004)]
    public void Parse_round_trips_formatted_values(int milliseconds)
    {
        var value = TimeSpan.FromMilliseconds(milliseconds);

        Assert.Equal(value, TimeShiftText.Parse(TimeShiftText.Format(value)));
    }

    [Fact]
    public void Parse_supports_two_digit_hours()
    {
        var fields = new TimeShiftFields(false, "99", "00", "00", "000");

        Assert.Equal(TimeSpan.FromHours(99), TimeShiftText.Parse(fields));
    }

    [Fact]
    public void Parse_clamps_out_of_range_parts()
    {
        var fields = new TimeShiftFields(false, "100", "60", "60", "1000");

        Assert.Equal(
            TimeSpan.FromHours(99) + TimeSpan.FromMinutes(59) +
            TimeSpan.FromSeconds(59) + TimeSpan.FromMilliseconds(999),
            TimeShiftText.Parse(fields));
    }

    [Theory]
    [InlineData(TimeShiftPart.Hours, "7", "07")]
    [InlineData(TimeShiftPart.Hours, "99", "99")]
    [InlineData(TimeShiftPart.Hours, "100", "99")]
    [InlineData(TimeShiftPart.Hours, "", "00")]
    [InlineData(TimeShiftPart.Hours, null, "00")]
    [InlineData(TimeShiftPart.Hours, "999999999999999999999", "00")]
    [InlineData(TimeShiftPart.Minutes, "9", "09")]
    [InlineData(TimeShiftPart.Minutes, "60", "59")]
    [InlineData(TimeShiftPart.Seconds, "5", "05")]
    [InlineData(TimeShiftPart.Seconds, "99", "59")]
    [InlineData(TimeShiftPart.Milliseconds, "7", "007")]
    [InlineData(TimeShiftPart.Milliseconds, "1000", "999")]
    public void NormalizePart_clamps_and_pads(
        TimeShiftPart part,
        string? text,
        string expected)
    {
        Assert.Equal(expected, TimeShiftText.NormalizePart(part, text));
    }
}

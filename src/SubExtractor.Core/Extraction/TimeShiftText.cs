namespace SubExtractor.Core.Extraction;

public enum TimeShiftPart
{
    Hours,
    Minutes,
    Seconds,
    Milliseconds,
}

public sealed record TimeShiftFields(
    bool IsNegative,
    string Hours,
    string Minutes,
    string Seconds,
    string Milliseconds);

public static class TimeShiftText
{
    public static TimeShiftFields Format(TimeSpan shift)
    {
        var negative = shift < TimeSpan.Zero;
        var absolute = negative ? shift.Negate() : shift;

        return new TimeShiftFields(
            negative,
            ((long)absolute.TotalHours).ToString("00"),
            absolute.Minutes.ToString("00"),
            absolute.Seconds.ToString("00"),
            absolute.Milliseconds.ToString("000"));
    }

    public static TimeSpan Parse(TimeShiftFields fields)
    {
        var hours = ParsePart(TimeShiftPart.Hours, fields.Hours);
        var minutes = ParsePart(TimeShiftPart.Minutes, fields.Minutes);
        var seconds = ParsePart(TimeShiftPart.Seconds, fields.Seconds);
        var milliseconds = ParsePart(TimeShiftPart.Milliseconds, fields.Milliseconds);
        var totalMilliseconds = milliseconds + 1000L * (seconds + 60L * (minutes + 60L * hours));
        var value = TimeSpan.FromTicks(totalMilliseconds * TimeSpan.TicksPerMillisecond);

        return fields.IsNegative ? value.Negate() : value;
    }

    public static string NormalizePart(TimeShiftPart part, string? text) =>
        ParsePart(part, text).ToString(part == TimeShiftPart.Milliseconds ? "000" : "00");

    private static int ParsePart(TimeShiftPart part, string? text)
    {
        var value = int.TryParse(text, out var parsed) ? parsed : 0;
        var max = part switch
        {
            TimeShiftPart.Hours => 99,
            TimeShiftPart.Minutes or TimeShiftPart.Seconds => 59,
            _ => 999,
        };

        return Math.Clamp(value, 0, max);
    }
}

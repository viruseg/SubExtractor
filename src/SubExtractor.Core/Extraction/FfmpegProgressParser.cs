using System.Globalization;

namespace SubExtractor.Core.Extraction;

public static class FfmpegProgressParser
{
    public static bool TryParsePosition(string line, out TimeSpan position)
    {
        position = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();

        switch (key)
        {
            case "out_time_us":
            case "out_time_ms":
                if (!long.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var microseconds) ||
                    microseconds < 0 ||
                    microseconds > TimeSpan.MaxValue.Ticks / 10)
                {
                    return false;
                }

                position = TimeSpan.FromTicks(microseconds * 10);
                return true;

            case "out_time":
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ||
                    parsed < TimeSpan.Zero)
                {
                    return false;
                }

                position = parsed;
                return true;

            default:
                return false;
        }
    }
}

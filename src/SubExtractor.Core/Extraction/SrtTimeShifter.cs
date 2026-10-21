using System.Text;
using System.Text.RegularExpressions;

namespace SubExtractor.Core.Extraction;

public static class SrtTimeShifter
{
    private const string TimestampPattern = @"-?\d+:\d{2}:\d{2}[,.]\d{3}";

    private static readonly Regex TimestampLine = new(
        $@"(?m)^\s*{TimestampPattern}\s*-->\s*{TimestampPattern}.*$",
        RegexOptions.Compiled);

    private static readonly Regex Timestamp = new(
        @"(?<negative>-)?(?<hours>\d+):(?<minutes>\d{2}):(?<seconds>\d{2})" +
        @"(?<separator>[,.])(?<milliseconds>\d{3})",
        RegexOptions.Compiled);

    public static string Shift(string content, TimeSpan shift)
    {
        if (shift == TimeSpan.Zero)
        {
            return content;
        }

        return TimestampLine.Replace(
            content,
            line => Timestamp.Replace(line.Value, match => ShiftTimestamp(match, shift)));
    }

    public static void ShiftFile(string path, TimeSpan shift)
    {
        if (shift == TimeSpan.Zero)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var (encoding, preambleLength) = DetectEncoding(bytes);
        var content = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        var shifted = Shift(content, shift);
        if (shifted == content)
        {
            return;
        }

        var output = encoding.GetBytes(shifted);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, preambleLength);
        stream.Write(output, 0, output.Length);
    }

    private static string ShiftTimestamp(Match match, TimeSpan shift)
    {
        var totalMilliseconds =
            ((long.Parse(match.Groups["hours"].Value) * 60
              + long.Parse(match.Groups["minutes"].Value)) * 60
             + long.Parse(match.Groups["seconds"].Value)) * 1000
            + long.Parse(match.Groups["milliseconds"].Value);

        if (match.Groups["negative"].Success)
        {
            totalMilliseconds = -totalMilliseconds;
        }

        totalMilliseconds += shift.Ticks / TimeSpan.TicksPerMillisecond;

        var sign = totalMilliseconds < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(totalMilliseconds);

        return string.Concat(
            sign,
            (absolute / 3_600_000).ToString("00"),
            ":",
            (absolute / 60_000 % 60).ToString("00"),
            ":",
            (absolute / 1000 % 60).ToString("00"),
            match.Groups["separator"].Value,
            (absolute % 1000).ToString("000"));
    }

    private static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes) =>
        bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => (Encoding.UTF8, 3),
            [0xFF, 0xFE, ..] => (Encoding.Unicode, 2),
            [0xFE, 0xFF, ..] => (Encoding.BigEndianUnicode, 2),
            _ => (Encoding.UTF8, 0),
        };
}

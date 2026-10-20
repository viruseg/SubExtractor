using System.Text.RegularExpressions;

namespace SubExtractor.Core.Ffmpeg;

public sealed record FfmpegVersion(string Display, Version? Numeric);

public static partial class FfmpegVersionParser
{
    private const string Marker = "ffmpeg version";

    [GeneratedRegex(@"^\d+(\.\d+){0,3}", RegexOptions.CultureInvariant)]
    private static partial Regex NumericPrefix();

    public static FfmpegVersion Parse(string versionOutput)
    {
        foreach (var line in versionOutput.Split('\n'))
        {
            var markerIndex = line.IndexOf(Marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                continue;
            }

            var tokens = line[(markerIndex + Marker.Length)..]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return new FfmpegVersion("", null);
            }

            var token = tokens[0];
            var match = NumericPrefix().Match(token);
            return match.Success && Version.TryParse(match.Value, out var numeric)
                ? new FfmpegVersion(match.Value, numeric)
                : new FfmpegVersion(token, null);
        }

        return new FfmpegVersion("", null);
    }
}

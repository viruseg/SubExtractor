using System.Text.RegularExpressions;

namespace SubExtractor.Core.Ffmpeg;

public static partial class VersionComparison
{
    [GeneratedRegex(@"\d+(\.\d+){0,3}", RegexOptions.CultureInvariant)]
    private static partial Regex NumericVersion();

    public static Version? ParseNumeric(string value)
    {
        var match = NumericVersion().Match(value);
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }

    public static bool IsNewer(string localOutput, string remoteVersion)
    {
        var local = ParseNumeric(localOutput);
        var remote = ParseNumeric(remoteVersion);
        return local is not null && remote is not null && remote > local;
    }
}

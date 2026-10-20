namespace SubExtractor.Core.Ffmpeg;

public static class ChecksumParser
{
    public static string? Parse(string content)
    {
        var token = content
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return token is { Length: 64 } && token.All(char.IsAsciiHexDigit)
            ? token.ToLowerInvariant()
            : null;
    }
}

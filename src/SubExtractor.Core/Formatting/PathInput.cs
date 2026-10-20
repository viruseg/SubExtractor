namespace SubExtractor.Core.Formatting;

public static class PathInput
{
    public static string Sanitize(string? input)
    {
        var value = (input ?? string.Empty).Trim();

        if (value.Length >= 2 && IsQuote(value[0]) && value[0] == value[^1])
            value = value[1..^1].Trim();

        return value;
    }

    public static bool IsExistingFolder(string? input) => Directory.Exists(Sanitize(input));

    private static bool IsQuote(char value) => value is '"' or '\'';
}

namespace SubExtractor.Core.Formatting;

public static class TrackKey
{
    public static string Build(string? title, string? language) =>
        $"{NormalizeTitle(title)}\u001F{NormalizeLanguage(language)}";

    public static string NormalizeTitle(string? title) =>
        (title ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "und" : language.Trim().ToLowerInvariant();
}

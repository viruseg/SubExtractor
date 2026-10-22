namespace SubExtractor.Core.Extraction;

public static class TextRuleList
{
    public static IReadOnlyList<string> Parse(string? text) =>
        text is null
            ? []
            : text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

    public static string Format(IEnumerable<string> rules) =>
        string.Join(Environment.NewLine, rules);
}

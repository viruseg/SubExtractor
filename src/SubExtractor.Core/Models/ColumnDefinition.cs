namespace SubExtractor.Core.Models;

public sealed record ColumnDefinition(string Key, string DisplayTitle, string DisplayLanguage)
{
    public string HeaderLabel => string.IsNullOrEmpty(DisplayTitle)
        ? DisplayLanguage
        : $"{DisplayTitle} ({DisplayLanguage})";
}

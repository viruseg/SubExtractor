namespace SubExtractor.Core.Formatting;

public static class FileNameSanitizer
{
    public static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length == 0 ? "und" : sanitized;
    }
}

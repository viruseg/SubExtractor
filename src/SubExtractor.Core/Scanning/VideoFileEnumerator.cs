namespace SubExtractor.Core.Scanning;

public static class VideoFileEnumerator
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".wmv", ".webm", ".ts",
        ".m2ts", ".mpg", ".mpeg", ".flv", ".ogv", ".3gp", ".vob",
    };

    public static IReadOnlyList<string> Enumerate(string folder)
        => Directory
            .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(file => VideoExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(file => Path.GetFileName(file) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

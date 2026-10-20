using SubExtractor.Core.Models;

namespace SubExtractor.Core.Formatting;

public static class ColumnBuilder
{
    public static IReadOnlyList<ColumnDefinition> Build(IEnumerable<VideoItem> videos)
    {
        var columns = new Dictionary<string, ColumnDefinition>(StringComparer.Ordinal);

        foreach (var video in videos)
        {
            foreach (var track in video.Tracks)
            {
                var key = TrackKey.Build(track.Title, track.Language);
                var title = track.Title?.Trim() ?? string.Empty;
                var language = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language.Trim();

                columns.TryAdd(key, new ColumnDefinition(key, title, language));
            }
        }

        return columns.Values
            .OrderBy(column => column.DisplayLanguage, StringComparer.OrdinalIgnoreCase)
            .ThenBy(column => column.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

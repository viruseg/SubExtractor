using System.Globalization;
using System.Text.Json;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Ffprobe;

public static class FfprobeJsonParser
{
    public static IReadOnlyList<SubtitleTrack> Parse(string json, string videoPath)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("streams", out var streams) ||
            streams.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SubtitleTrack>();
        }

        var tracks = new List<SubtitleTrack>();
        var duration = ReadDuration(document.RootElement);
        foreach (var stream in streams.EnumerateArray())
        {
            if (stream.ValueKind != JsonValueKind.Object ||
                !stream.TryGetProperty("index", out var indexElement) ||
                !indexElement.TryGetInt32(out var index))
            {
                continue;
            }

            var codecName = stream.TryGetProperty("codec_name", out var codecElement) &&
                            codecElement.ValueKind == JsonValueKind.String
                ? codecElement.GetString() ?? string.Empty
                : string.Empty;

            string? title = null;
            string? language = null;
            if (stream.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
            {
                title = ReadString(tags, "title");
                language = ReadString(tags, "language");
            }

            tracks.Add(new SubtitleTrack(videoPath, index, codecName, title, language, duration));
        }

        return tracks;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static TimeSpan? ReadDuration(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var format) ||
            format.ValueKind != JsonValueKind.Object ||
            !format.TryGetProperty("duration", out var duration))
        {
            return null;
        }

        double seconds;
        if (duration.ValueKind == JsonValueKind.String)
        {
            if (!double.TryParse(
                    duration.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out seconds))
            {
                return null;
            }
        }
        else if (duration.ValueKind == JsonValueKind.Number)
        {
            if (!duration.TryGetDouble(out seconds))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        return seconds > 0 &&
               double.IsFinite(seconds) &&
               seconds <= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }
}

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

            tracks.Add(new SubtitleTrack(videoPath, index, codecName, title, language));
        }

        return tracks;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

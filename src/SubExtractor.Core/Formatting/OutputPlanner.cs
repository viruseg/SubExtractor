using SubExtractor.Core.Models;

namespace SubExtractor.Core.Formatting;

public static class OutputPlanner
{
    public static IReadOnlyList<OutputPlan> Plan(
        string folder,
        IEnumerable<SubtitleTrack> selectedTracks,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<OutputPlan>();

        foreach (var track in selectedTracks
                     .OrderBy(track => Path.GetFileName(track.VideoPath), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(track => track.StreamIndex))
        {
            var format = SubtitleFormats.For(track.CodecName);
            var language = FileNameSanitizer.Sanitize(
                string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language);
            var stem = $"{Path.GetFileNameWithoutExtension(track.VideoPath)}.{language}";
            var finalPath = Claim(folder, stem, track.StreamIndex, format.Extension, claimed);

            plans.Add(new OutputPlan(
                track,
                finalPath,
                format.Extension,
                format.FfmpegFormat,
                format.Mode,
                fileExists(finalPath)));
        }

        return plans;
    }

    private static string Claim(
        string folder,
        string stem,
        int streamIndex,
        string extension,
        HashSet<string> claimed)
    {
        var path = Path.Combine(folder, stem + extension);
        if (claimed.Add(path))
        {
            return path;
        }

        path = Path.Combine(folder, $"{stem}.{streamIndex}{extension}");
        if (claimed.Add(path))
        {
            return path;
        }

        for (var suffix = 2; ; suffix++)
        {
            path = Path.Combine(folder, $"{stem}.{streamIndex}-{suffix}{extension}");
            if (claimed.Add(path))
            {
                return path;
            }
        }
    }
}

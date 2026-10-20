using SubExtractor.Core.Models;

namespace SubExtractor.Core.Formatting;

public static class SubtitleFormats
{
    public static SubtitleFormat For(string? codecName) => codecName?.Trim().ToLowerInvariant() switch
    {
        "subrip" => new SubtitleFormat(".srt", "srt", SubtitleWriteMode.Copy),
        "ass" or "ssa" => new SubtitleFormat(".ass", "ass", SubtitleWriteMode.Copy),
        "webvtt" => new SubtitleFormat(".vtt", "webvtt", SubtitleWriteMode.Copy),
        "mov_text" or "text" => new SubtitleFormat(".srt", "srt", SubtitleWriteMode.EncodeSrt),
        "hdmv_pgs_subtitle" => new SubtitleFormat(".sup", "sup", SubtitleWriteMode.Copy),
        "dvd_subtitle" => new SubtitleFormat(".mks", "matroska", SubtitleWriteMode.Copy),
        _ => new SubtitleFormat(".srt", "srt", SubtitleWriteMode.EncodeSrt),
    };
}

namespace SubExtractor.Core.Models;

public sealed record OutputPlan(
    SubtitleTrack Track,
    string FinalPath,
    string Extension,
    string FfmpegFormat,
    SubtitleWriteMode Mode,
    bool OverwritesExisting);

namespace SubExtractor.Core.Models;

public sealed record VideoItem(
    string FilePath,
    ProbeStatus Status,
    IReadOnlyList<SubtitleTrack> Tracks,
    string? Error);

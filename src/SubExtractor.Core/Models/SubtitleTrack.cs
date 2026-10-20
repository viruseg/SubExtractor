namespace SubExtractor.Core.Models;

public sealed record SubtitleTrack(
    string VideoPath,
    int StreamIndex,
    string CodecName,
    string? Title,
    string? Language);

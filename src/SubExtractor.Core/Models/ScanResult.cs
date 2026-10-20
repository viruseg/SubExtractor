namespace SubExtractor.Core.Models;

public sealed record ScanResult(IReadOnlyList<VideoItem> Videos, string? FolderError);

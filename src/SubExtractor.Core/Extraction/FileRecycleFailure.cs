namespace SubExtractor.Core.Extraction;

public sealed record FileRecycleFailure(
    string Path,
    string Stage,
    int HResult,
    int Attempts);

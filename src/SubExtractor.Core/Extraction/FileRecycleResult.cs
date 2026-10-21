namespace SubExtractor.Core.Extraction;

public sealed record FileRecycleResult(IReadOnlyList<FileRecycleFailure> Failures);

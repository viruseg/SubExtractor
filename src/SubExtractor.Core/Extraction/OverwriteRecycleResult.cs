using SubExtractor.Core.Models;

namespace SubExtractor.Core.Extraction;

public sealed record OverwriteRecycleResult(
    IReadOnlyList<OutputPlan> ExtractablePlans,
    IReadOnlyList<FileRecycleFailure> Failures);

using SubExtractor.Core.Models;

namespace SubExtractor.Core.Extraction;

public sealed class OverwriteRecycler(IFileRecycler recycler)
{
    public async Task<OverwriteRecycleResult> RecycleAsync(
        IReadOnlyList<OutputPlan> plans,
        CancellationToken cancellationToken)
    {
        var targets = plans
            .Where(plan => plan.OverwritesExisting)
            .Select(plan => plan.FinalPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            return new OverwriteRecycleResult(plans, []);
        }

        var result = await recycler
            .RecycleAsync(targets, cancellationToken)
            .ConfigureAwait(false);

        var failures = result.Failures
            .Where(failure => File.Exists(failure.Path))
            .ToList();

        if (failures.Count == 0)
        {
            return new OverwriteRecycleResult(plans, []);
        }

        var blocked = failures
            .Select(failure => failure.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extractable = plans
            .Where(plan => !blocked.Contains(plan.FinalPath))
            .ToList();

        return new OverwriteRecycleResult(extractable, failures);
    }
}

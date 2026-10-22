using SubExtractor.Core.Models;

namespace SubExtractor.Core.Extraction;

public static class ExtractionReport
{
    public static bool HasOverwrites(IReadOnlyList<OutputPlan> plans) =>
        plans.Any(plan => plan.OverwritesExisting);

    public static IReadOnlyList<string> RecycleFailureMessages(
        IReadOnlyList<FileRecycleFailure> failures) =>
        failures
            .Select(failure =>
                $"Could not move '{Path.GetFileName(failure.Path)}' to the Recycle Bin: " +
                $"{failure.Stage} (0x{failure.HResult:X8}) after {failure.Attempts} attempt(s). " +
                "This subtitle will be skipped.")
            .ToList();

    public static IReadOnlyList<string> SrtOnlySkipMessages(
        IReadOnlyList<OutputPlan> plans,
        bool appliesTimeShift,
        bool appliesTextRemoval)
    {
        if (!appliesTimeShift && !appliesTextRemoval)
        {
            return [];
        }

        var operations = (appliesTimeShift, appliesTextRemoval) switch
        {
            (true, true) => "Time shift and text removal are",
            (true, false) => "Time shift is",
            _ => "Text removal is",
        };

        return plans
            .Where(plan => !plan.Extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
            .Select(plan =>
                $"{operations} only applied to SRT subtitles; skipped '{Path.GetFileName(plan.FinalPath)}'.")
            .ToList();
    }

    public static string SummaryText(ExtractionSummary summary) =>
        $"Extraction finished: {summary.Succeeded} succeeded, {summary.Failed} failed.";
}

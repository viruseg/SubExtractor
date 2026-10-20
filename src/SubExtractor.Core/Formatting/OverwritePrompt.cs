using SubExtractor.Core.Models;

namespace SubExtractor.Core.Formatting;

public static class OverwritePrompt
{
    private const int MaxListedFiles = 10;

    public static IReadOnlyList<string> BuildLines(IEnumerable<OutputPlan> plans)
    {
        var names = plans
            .Where(plan => plan.OverwritesExisting)
            .Select(plan => Path.GetFileName(plan.FinalPath))
            .ToList();

        if (names.Count <= MaxListedFiles)
        {
            return names;
        }

        var lines = names.Take(MaxListedFiles).ToList();
        lines.Add($"and {names.Count - MaxListedFiles} more");
        return lines;
    }
}

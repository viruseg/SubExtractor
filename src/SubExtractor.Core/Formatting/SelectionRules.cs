namespace SubExtractor.Core.Formatting;

public static class SelectionRules
{
    public static bool? ComputeState(IEnumerable<bool> selected)
    {
        var any = false;
        var all = true;
        var count = 0;

        foreach (var value in selected)
        {
            count++;
            any |= value;
            all &= value;
        }

        if (count == 0 || !any)
        {
            return false;
        }

        return all ? true : null;
    }

    public static bool ShouldSelectAll(IEnumerable<bool> selected) => selected.Any(value => !value);
}

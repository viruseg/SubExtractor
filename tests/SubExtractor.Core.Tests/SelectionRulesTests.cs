using SubExtractor.Core.Formatting;

namespace SubExtractor.Core.Tests;

public class SelectionRulesTests
{
    public static TheoryData<bool[], bool?> StateCases => new()
    {
        { Array.Empty<bool>(), false },
        { [false, false], false },
        { [true, true], true },
        { [true, false], null },
        { [false, true], null },
    };

    public static TheoryData<bool[], bool> SelectAllCases => new()
    {
        { Array.Empty<bool>(), false },
        { [false, false], true },
        { [true, true], false },
        { [true, false], true },
    };

    [Theory]
    [MemberData(nameof(StateCases))]
    public void ComputeState_returns_all_none_or_mixed(bool[] selected, bool? expected)
        => Assert.Equal(expected, SelectionRules.ComputeState(selected));

    [Theory]
    [MemberData(nameof(SelectAllCases))]
    public void ShouldSelectAll_checks_when_any_cell_is_unchecked(bool[] selected, bool expected)
        => Assert.Equal(expected, SelectionRules.ShouldSelectAll(selected));
}

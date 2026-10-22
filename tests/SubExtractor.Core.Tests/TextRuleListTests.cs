using SubExtractor.Core.Extraction;

namespace SubExtractor.Core.Tests;

public sealed class TextRuleListTests
{
    [Fact]
    public void Parse_handles_crlf_lf_and_cr()
    {
        var rules = TextRuleList.Parse("one\r\ntwo\nthree\rfour");

        Assert.Equal(["one", "two", "three", "four"], rules);
    }

    [Fact]
    public void Parse_preserves_empty_entries()
    {
        Assert.Equal(["", ""], TextRuleList.Parse("\r\n"));
        Assert.Equal([""], TextRuleList.Parse(""));
        Assert.Empty(TextRuleList.Parse(null));
    }

    [Fact]
    public void Format_joins_with_the_environment_newline()
    {
        Assert.Equal($"one{Environment.NewLine}two", TextRuleList.Format(["one", "two"]));
        Assert.Equal(string.Empty, TextRuleList.Format([]));
    }

    [Fact]
    public void Parse_and_Format_round_trip()
    {
        var rules = new[] { "one", "", "two" };

        Assert.Equal(rules, TextRuleList.Parse(TextRuleList.Format(rules)));
    }
}

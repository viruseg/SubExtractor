using System.Text;
using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class SrtTextRemoverTests
{
    private const string Header = "1\n00:00:11,052 --> 00:00:12,251\n";

    [Theory]
    [InlineData("Текст субтитров", "Текст субтитров")]
    [InlineData("  ТЕКСТ СУБТИТРОВ  ", "текст субтитров")]
    public void Full_match_removes_the_whole_text_line(string text, string rule)
    {
        var content = Header + text + "\n";

        var result = SrtTextRemover.Remove(content, [rule]);

        Assert.Equal(Header + "\n", result);
    }

    [Theory]
    [InlineData("Текст субтитров", "@ субтитров", "Текст")]
    [InlineData("Текст субтитров", "@субтитров", "Текст ")]
    [InlineData("Текст субтитров", "@СУБТИТРОВ", "Текст ")]
    [InlineData("foo foo FOO", "@foo", "  ")]
    [InlineData("Напиши @username сюда", "@@username", "Напиши  сюда")]
    public void Partial_replace_removes_matches_without_trimming(string text, string rule, string expectedText)
    {
        var content = Header + text + "\n";

        var result = SrtTextRemover.Remove(content, [rule]);

        Assert.Equal(Header + expectedText + "\n", result);
    }

    [Theory]
    [InlineData("Текст субтитров", "#субтитров")]
    [InlineData("Текст #субтитров", "##субтитров")]
    [InlineData("ТЕКСТ СУБТИТРОВ", "#субтитров")]
    public void Hash_rules_remove_the_whole_matching_line(string text, string rule)
    {
        var content = Header + text + "\n";

        var result = SrtTextRemover.Remove(content, [rule]);

        Assert.Equal(Header + "\n", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("@")]
    [InlineData("#")]
    [InlineData("@ ")]
    [InlineData("@  ")]
    [InlineData("# ")]
    [InlineData("#  ")]
    public void Blank_and_sigil_only_rules_are_ignored(string rule)
    {
        const string content = Header + "Текст субтитров\n";

        Assert.Equal(content, SrtTextRemover.Remove(content, [rule]));
    }

    [Fact]
    public void Partial_replace_does_not_trim_the_search_text()
    {
        const string content = Header + "Text\n";

        Assert.Equal(content, SrtTextRemover.Remove(content, ["@ Text"]));
    }

    [Fact]
    public void Full_match_wins_over_partial_replacement()
    {
        const string content = Header + "Текст субтитров\n";

        var result = SrtTextRemover.Remove(content, ["@Текст", "Текст субтитров"]);

        Assert.Equal(Header + "\n", result);
    }

    [Fact]
    public void Hash_rule_wins_over_partial_replacement()
    {
        const string content = Header + "Текст субтитров\n";

        var result = SrtTextRemover.Remove(content, ["@Текст", "#субтитров"]);

        Assert.Equal(Header + "\n", result);
    }

    [Fact]
    public void Deleted_line_is_removed_from_a_multiline_cue()
    {
        const string content =
            "1\n00:00:17,186 --> 00:00:20,083\nЧтобы складывать\nи вычитать одночлены,\n";

        var result = SrtTextRemover.Remove(content, ["и вычитать одночлены,"]);

        Assert.Equal(
            "1\n00:00:17,186 --> 00:00:20,083\nЧтобы складывать\n",
            result);
    }

    [Fact]
    public void All_lines_deleted_from_a_multiline_cue_leave_one_empty_line()
    {
        const string content =
            "1\n00:00:17,186 --> 00:00:20,083\nЧтобы складывать\nи вычитать одночлены,\n";

        var result = SrtTextRemover.Remove(
            content,
            ["Чтобы складывать", "и вычитать одночлены,"]);

        Assert.Equal("1\n00:00:17,186 --> 00:00:20,083\n\n", result);
    }

    [Fact]
    public void Empty_cue_before_another_cue_reuses_the_existing_separator()
    {
        const string content =
            "1\n00:00:01,000 --> 00:00:02,000\nFirst\n\n" +
            "2\n00:00:03,000 --> 00:00:04,000\nSecond\n";

        var result = SrtTextRemover.Remove(content, ["First"]);

        Assert.Equal(
            "1\n00:00:01,000 --> 00:00:02,000\n\n" +
            "2\n00:00:03,000 --> 00:00:04,000\nSecond\n",
            result);
    }

    [Fact]
    public void Partially_emptied_line_is_dropped_from_a_multiline_cue()
    {
        const string content = Header + "A\nB\n";

        var result = SrtTextRemover.Remove(content, ["@A"]);

        Assert.Equal(Header + "B\n", result);
    }

    [Fact]
    public void Text_line_that_looks_like_a_timestamp_is_still_treated_as_text()
    {
        const string content = Header + "00:00:01,000 --> 00:00:02,000\n";

        var result = SrtTextRemover.Remove(content, ["00:00:01,000 --> 00:00:02,000"]);

        Assert.Equal(Header + "\n", result);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00:00:11,052 --> 00:00:12,251")]
    public void Rules_do_not_touch_indices_or_timestamps(string rule)
    {
        const string content = Header + "Text\n";

        Assert.Equal(content, SrtTextRemover.Remove(content, [rule]));
    }

    [Fact]
    public void No_matching_rules_leave_content_unchanged()
    {
        const string content = Header + "Text\n";

        Assert.Equal(content, SrtTextRemover.Remove(content, ["Other"]));
    }

    [Fact]
    public void Remove_preserves_crlf_and_leaves_one_empty_line()
    {
        const string content = "1\r\n00:00:01,000 --> 00:00:02,000\r\nText\r\n";

        var result = SrtTextRemover.Remove(content, ["Text"]);

        Assert.Equal("1\r\n00:00:01,000 --> 00:00:02,000\r\n\r\n", result);
    }

    [Fact]
    public void Remove_preserves_crlf_and_existing_separator()
    {
        const string content =
            "1\r\n00:00:01,000 --> 00:00:02,000\r\nText\r\n\r\n" +
            "2\r\n00:00:03,000 --> 00:00:04,000\r\nOther\r\n";

        var result = SrtTextRemover.Remove(content, ["Text"]);

        Assert.Equal(
            "1\r\n00:00:01,000 --> 00:00:02,000\r\n\r\n" +
            "2\r\n00:00:03,000 --> 00:00:04,000\r\nOther\r\n",
            result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemoveFile_preserves_utf8_bom(bool withBom)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var content = "1\r\n00:00:05,052 --> 00:00:06,251\r\nПривет\r\n";
        var preamble = withBom ? Encoding.UTF8.GetPreamble() : [];
        File.WriteAllBytes(path, [.. preamble, .. Encoding.UTF8.GetBytes(content)]);

        SrtTextRemover.RemoveFile(path, ["Привет"]);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(preamble, bytes[..preamble.Length]);
        Assert.Equal(
            "1\r\n00:00:05,052 --> 00:00:06,251\r\n\r\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void RemoveFile_preserves_utf16_encoding()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var content = "1\r\n00:00:05,052 --> 00:00:06,251\r\nПривет\r\n";
        var preamble = Encoding.Unicode.GetPreamble();
        File.WriteAllBytes(path, [.. preamble, .. Encoding.Unicode.GetBytes(content)]);

        SrtTextRemover.RemoveFile(path, ["Привет"]);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(preamble, bytes[..preamble.Length]);
        Assert.Equal(
            "1\r\n00:00:05,052 --> 00:00:06,251\r\n\r\n",
            Encoding.Unicode.GetString(bytes, preamble.Length, bytes.Length - preamble.Length));
    }

    [Fact]
    public void RemoveFile_without_rules_leaves_file_bytes_untouched()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var bytes = Encoding.UTF8.GetBytes("1\n00:00:05,052 --> 00:00:06,251\nText\n");
        File.WriteAllBytes(path, bytes);

        SrtTextRemover.RemoveFile(path, []);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void RemoveFile_with_no_matching_rules_leaves_file_bytes_untouched()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Movie.eng.srt");
        var bytes = Encoding.UTF8.GetBytes("1\n00:00:05,052 --> 00:00:06,251\nText\n");
        File.WriteAllBytes(path, bytes);

        SrtTextRemover.RemoveFile(path, ["Other"]);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
}

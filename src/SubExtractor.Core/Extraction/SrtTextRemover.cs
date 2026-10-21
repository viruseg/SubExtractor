using System.Text.RegularExpressions;

namespace SubExtractor.Core.Extraction;

public static class SrtTextRemover
{
    private static readonly Regex TimestampLine = new(
        @"^\s*-?\d+:\d{2}:\d{2}[,.]\d{3}\s*-->\s*-?\d+:\d{2}:\d{2}[,.]\d{3}.*$",
        RegexOptions.Compiled);

    public static string Remove(string content, IReadOnlyList<string> rules)
    {
        var parsed = ParseRules(rules);
        if (parsed.Count == 0)
        {
            return content;
        }

        var lines = SplitLines(content);
        var output = new List<Line>(lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            output.Add(line);

            if (!TimestampLine.IsMatch(line.Text))
            {
                continue;
            }

            var textStart = index + 1;
            var textEnd = textStart;
            while (textEnd < lines.Count && !IsBlank(lines[textEnd].Text))
            {
                textEnd++;
            }

            if (textEnd == textStart)
            {
                continue;
            }

            var remaining = ProcessTextLines(lines, textStart, textEnd, parsed);
            if (remaining.Count > 0)
            {
                output.AddRange(remaining);
            }
            else
            {
                var nextIsBlank = textEnd < lines.Count && IsBlank(lines[textEnd].Text);
                if (!nextIsBlank)
                {
                    output.Add(new Line(string.Empty, line.Ending.Length > 0 ? line.Ending : "\n"));
                }
            }

            index = textEnd - 1;
        }

        return string.Concat(output.Select(line => line.Text + line.Ending));
    }

    public static void RemoveFile(string path, IReadOnlyList<string> rules)
    {
        if (rules.Count == 0)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var (encoding, preambleLength) = SrtFileEncoding.Detect(bytes);
        var content = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        var cleaned = Remove(content, rules);
        if (cleaned == content)
        {
            return;
        }

        var output = encoding.GetBytes(cleaned);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, preambleLength);
        stream.Write(output, 0, output.Length);
    }

    private static List<Line> ProcessTextLines(List<Line> lines, int start, int end, List<Rule> rules)
    {
        var fullLineRules = rules.Where(rule => rule.Kind == RuleKind.FullLine).ToArray();
        var deleteLineRules = rules.Where(rule => rule.Kind == RuleKind.DeleteLine).ToArray();
        var replaceRules = rules.Where(rule => rule.Kind == RuleKind.Replace).ToArray();

        var result = new List<Line>(end - start);
        for (var index = start; index < end; index++)
        {
            var line = lines[index];
            var trimmed = line.Text.Trim();

            if (fullLineRules.Any(rule => trimmed.Equals(rule.Text, StringComparison.OrdinalIgnoreCase)) ||
                deleteLineRules.Any(rule => line.Text.Contains(rule.Text, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var text = line.Text;
            foreach (var rule in replaceRules)
            {
                text = text.Replace(rule.Text, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            if (text.Length == 0)
            {
                continue;
            }

            result.Add(new Line(text, line.Ending));
        }

        return result;
    }

    private static List<Rule> ParseRules(IReadOnlyList<string> rules)
    {
        var result = new List<Rule>();
        foreach (var raw in rules)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (raw[0] == '@')
            {
                var text = raw[1..];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new Rule(RuleKind.Replace, text));
                }

                continue;
            }

            if (raw[0] == '#')
            {
                var text = raw[1..];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new Rule(RuleKind.DeleteLine, text));
                }

                continue;
            }

            var fullText = raw.Trim();
            if (fullText.Length > 0)
            {
                result.Add(new Rule(RuleKind.FullLine, fullText));
            }
        }

        return result;
    }

    private static List<Line> SplitLines(string content)
    {
        var lines = new List<Line>();
        var index = 0;

        while (index < content.Length)
        {
            var start = index;
            while (index < content.Length && content[index] is not ('\r' or '\n'))
            {
                index++;
            }

            var text = content[start..index];
            var ending = string.Empty;

            if (index < content.Length)
            {
                if (content[index] == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    ending = "\r\n";
                    index += 2;
                }
                else
                {
                    ending = content[index].ToString();
                    index++;
                }
            }

            lines.Add(new Line(text, ending));
        }

        return lines;
    }

    private static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);

    private enum RuleKind
    {
        FullLine,
        Replace,
        DeleteLine,
    }

    private readonly record struct Rule(RuleKind Kind, string Text);

    private readonly record struct Line(string Text, string Ending);
}

namespace SubExtractor.Core.Formatting;

public static class ColumnWidthPolicy
{
    public const double MinWidth = 104;
    public const double MaxWidth = 210;
    public const double HorizontalPadding = 24;
    public const double FileCellGap = 8;

    private const double TitleFontSize = 13;
    private const double TitleWeightFactor = 1.04;
    private const double LanguageFontSize = 10;
    private const double LanguageLetterSpacing = 0.4;
    private const double FileNameFontSize = 13;
    private const double StatusFontSize = 12;

    public static double Measure(string title, string language)
    {
        var titleWidth = Estimate(title, TitleFontSize, TitleWeightFactor);
        var languageWidth = Estimate(
                language.ToUpperInvariant(),
                LanguageFontSize,
                1.0) +
            (language.Length > 1 ? LanguageLetterSpacing * (language.Length - 1) : 0);

        return Math.Clamp(
            Math.Max(titleWidth, languageWidth) + HorizontalPadding,
            MinWidth,
            MaxWidth);
    }

    public static double MeasureFileCell(string fileName, string? status)
    {
        var nameWidth = Estimate(fileName, FileNameFontSize, 1.0);
        var statusWidth = string.IsNullOrEmpty(status)
            ? 0
            : Estimate(status, StatusFontSize, 1.0) + FileCellGap;

        return nameWidth + statusWidth + HorizontalPadding;
    }

    private static double Estimate(string text, double fontSize, double weightFactor)
    {
        var em = 0d;
        foreach (var ch in text)
        {
            em += Advance(ch);
        }

        return em * fontSize * weightFactor;
    }

    private static double Advance(char ch)
    {
        if (ch == ' ')
        {
            return 0.28;
        }

        if ("iljI|!.,:;'".IndexOf(ch) >= 0)
        {
            return 0.30;
        }

        if ("mwMW@%".IndexOf(ch) >= 0)
        {
            return 0.90;
        }

        if (ch is >= 'A' and <= 'Z')
        {
            return 0.66;
        }

        if (ch is >= '0' and <= '9')
        {
            return 0.56;
        }

        if (ch is >= '\u0400' and <= '\u04FF')
        {
            return 0.56;
        }

        return ch >= '\u4E00' ? 1.0 : 0.52;
    }
}

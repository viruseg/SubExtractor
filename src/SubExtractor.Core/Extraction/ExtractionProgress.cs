namespace SubExtractor.Core.Extraction;

public static class ExtractionProgress
{
    public static double Fraction(int completedTracks, double currentFraction, int totalTracks)
    {
        if (totalTracks <= 0)
        {
            return 0;
        }

        var clampedCurrent = Math.Clamp(currentFraction, 0, 1);
        return Math.Clamp((completedTracks + clampedCurrent) / totalTracks, 0, 1);
    }

    public static int LivePercent(double fraction) =>
        Math.Min(
            99,
            (int)Math.Round(
                Math.Clamp(fraction, 0, 1) * 100.0, MidpointRounding.AwayFromZero));

    public static int FinishedPercent(double completedTracks, int totalTracks)
    {
        if (totalTracks <= 0)
        {
            return 0;
        }

        var percent = completedTracks / totalTracks * 100.0;
        if (completedTracks < totalTracks)
        {
            percent = Math.Min(percent, 99.0);
        }

        return (int)Math.Round(Math.Clamp(percent, 0, 100), MidpointRounding.AwayFromZero);
    }

    public static double OverallPercent(double fraction, bool live)
    {
        var clamped = Math.Clamp(fraction, 0, 1);
        return !live && clamped >= 1.0
            ? 100.0
            : Math.Min(clamped * 100.0, 99.0);
    }
}

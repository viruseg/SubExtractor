using SubExtractor.Core.Extraction;

namespace SubExtractor.Core.Tests;

public class ExtractionProgressTests
{
    [Theory]
    [InlineData(0, 0.0, 3, 0.0)]
    [InlineData(1, 0.5, 3, 0.5)]
    [InlineData(2, 1.0, 3, 1.0)]
    [InlineData(0, -1.0, 3, 0.0)]
    [InlineData(0, 2.0, 3, 1.0 / 3.0)]
    [InlineData(0, 0.5, 0, 0.0)]
    public void Fraction_combines_completed_tracks_and_current_track(
        int completed, double current, int total, double expected)
        => Assert.Equal(expected, ExtractionProgress.Fraction(completed, current, total), 6);

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.334, 33)]
    [InlineData(0.999, 99)]
    [InlineData(1.0, 99)]
    public void LivePercent_never_reaches_100(double fraction, int expected)
        => Assert.Equal(expected, ExtractionProgress.LivePercent(fraction));

    [Theory]
    [InlineData(0.0, 3, 0)]
    [InlineData(1.0, 3, 33)]
    [InlineData(2.0, 3, 67)]
    [InlineData(3.0, 3, 100)]
    [InlineData(0.0, 0, 0)]
    public void FinishedPercent_rounds_and_can_reach_100(
        double completed, int total, int expected)
        => Assert.Equal(expected, ExtractionProgress.FinishedPercent(completed, total));

    [Fact]
    public void Fraction_is_monotonic_across_track_boundaries()
    {
        var values = Enumerable.Range(0, 3)
            .SelectMany(completed => new[] { 0.0, 0.25, 0.5, 0.75, 0.999, 1.0 }
                .Select(fraction => ExtractionProgress.Fraction(completed, fraction, 3)))
            .ToList();

        Assert.Equal(values.Order(), values);
        Assert.Equal(1.0, values[^1]);
    }
}

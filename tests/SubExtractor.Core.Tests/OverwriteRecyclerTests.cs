using SubExtractor.Core.Extraction;
using SubExtractor.Core.Models;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class OverwriteRecyclerTests
{
    [Fact]
    public async Task Recycle_sends_only_replaced_files_to_the_recycler()
    {
        using var temp = new TempDirectory();
        var replaced = Path.Combine(temp.Path, "Movie.eng.srt");
        var fresh = Path.Combine(temp.Path, "Movie.rus.srt");
        File.WriteAllText(replaced, "OLD");
        var plans = new[]
        {
            Plan(replaced, overwrites: true),
            Plan(fresh, overwrites: false),
        };
        var recycler = new FakeFileRecycler();

        var result = await new OverwriteRecycler(recycler).RecycleAsync(plans, CancellationToken.None);

        Assert.Equal([replaced], recycler.Paths);
        Assert.Equal([replaced, fresh], result.ExtractablePlans.Select(plan => plan.FinalPath));
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task Recycle_skips_only_the_files_that_are_still_on_disk_after_a_failure()
    {
        using var temp = new TempDirectory();
        var blocked = Path.Combine(temp.Path, "Movie.eng.srt");
        var recycled = Path.Combine(temp.Path, "Movie.rus.srt");
        File.WriteAllText(blocked, "OLD");
        File.WriteAllText(recycled, "OLD");
        var plans = new[] { Plan(blocked, overwrites: true), Plan(recycled, overwrites: true) };
        var recycler = new FakeFileRecycler();
        recycler.Refuse.Add(blocked);

        var result = await new OverwriteRecycler(recycler).RecycleAsync(plans, CancellationToken.None);

        Assert.Equal([recycled], result.ExtractablePlans.Select(plan => plan.FinalPath));
        var failure = Assert.Single(result.Failures);
        Assert.Equal(blocked, failure.Path);
        Assert.Equal("Performing", failure.Stage);
        Assert.Equal(6, failure.Attempts);
    }

    [Fact]
    public async Task Recycle_ignores_a_failure_for_a_file_that_is_already_gone()
    {
        using var temp = new TempDirectory();
        var vanished = Path.Combine(temp.Path, "Movie.eng.srt");
        var plans = new[] { Plan(vanished, overwrites: true) };
        var recycler = new FakeFileRecycler();
        recycler.Refuse.Add(vanished);

        var result = await new OverwriteRecycler(recycler).RecycleAsync(plans, CancellationToken.None);

        Assert.Equal([vanished], result.ExtractablePlans.Select(plan => plan.FinalPath));
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task Recycle_does_not_call_the_recycler_when_nothing_is_replaced()
    {
        using var temp = new TempDirectory();
        var fresh = Path.Combine(temp.Path, "Movie.eng.srt");
        var plans = new[] { Plan(fresh, overwrites: false) };
        var recycler = new FakeFileRecycler();

        var result = await new OverwriteRecycler(recycler).RecycleAsync(plans, CancellationToken.None);

        Assert.Empty(recycler.Paths);
        Assert.Single(result.ExtractablePlans);
        Assert.Empty(result.Failures);
    }

    private static OutputPlan Plan(string finalPath, bool overwrites) =>
        new(
            new SubtitleTrack(
                Path.Combine(Path.GetDirectoryName(finalPath)!, "Movie.mkv"),
                2,
                "subrip",
                null,
                "eng"),
            finalPath,
            ".srt",
            "srt",
            SubtitleWriteMode.Copy,
            overwrites);

    private sealed class FakeFileRecycler : IFileRecycler
    {
        public List<string> Paths { get; } = [];

        public HashSet<string> Refuse { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<FileRecycleResult> RecycleAsync(
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken)
        {
            Paths.AddRange(paths);
            var failures = new List<FileRecycleFailure>();

            foreach (var path in paths)
            {
                if (Refuse.Contains(path))
                {
                    failures.Add(new FileRecycleFailure(path, "Performing", unchecked((int)0x80270027), 6));
                    continue;
                }

                File.Delete(path);
            }

            return Task.FromResult(new FileRecycleResult(failures));
        }
    }
}

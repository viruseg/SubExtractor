using SubExtractor.Core.Ffprobe;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Tests.TestHelpers;

public sealed class FakeSubtitleProbe(Func<string, IReadOnlyList<SubtitleTrack>> probe) : ISubtitleProbe
{
    public Task<IReadOnlyList<SubtitleTrack>> ProbeAsync(string videoPath, CancellationToken cancellationToken)
        => Task.FromResult(probe(videoPath));
}

using SubExtractor.Core.Models;

namespace SubExtractor.Core.Ffprobe;

public interface ISubtitleProbe
{
    Task<IReadOnlyList<SubtitleTrack>> ProbeAsync(string videoPath, CancellationToken cancellationToken);
}

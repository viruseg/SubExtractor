using SubExtractor.Core.Models;
using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Ffprobe;

public sealed class FfprobeService : ISubtitleProbe
{
    private readonly IProcessRunner _processRunner;
    private readonly string _ffprobePath;

    public FfprobeService(IProcessRunner processRunner, string ffprobePath)
    {
        _processRunner = processRunner;
        _ffprobePath = ffprobePath;
    }

    public async Task<IReadOnlyList<SubtitleTrack>> ProbeAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        var arguments = new[]
        {
            "-v", "error",
            "-select_streams", "s",
            "-show_entries", "stream=index,codec_name:stream_tags=title,language:format=duration",
            "-of", "json",
            "-i", videoPath,
        };

        var result = await _processRunner
            .RunAsync(_ffprobePath, arguments, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffprobe failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }

        return FfprobeJsonParser.Parse(result.StandardOutput, videoPath);
    }
}

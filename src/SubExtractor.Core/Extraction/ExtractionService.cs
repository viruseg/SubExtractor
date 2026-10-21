using SubExtractor.Core.Models;
using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Extraction;

public sealed class ExtractionService
{
    private readonly IProcessRunner _processRunner;
    private readonly string _ffmpegPath;

    public ExtractionService(IProcessRunner processRunner, string ffmpegPath)
    {
        _processRunner = processRunner;
        _ffmpegPath = ffmpegPath;
    }

    public async Task<ExtractionSummary> ExtractAsync(
        IReadOnlyList<OutputPlan> plans,
        TimeSpan subtitleTimeShift,
        IExtractionProgressSink progress,
        CancellationToken cancellationToken)
    {
        var totalOverall = plans.Count;
        var completedOverall = 0;
        var succeeded = 0;
        var failed = 0;

        foreach (var file in plans.GroupBy(plan => plan.Track.VideoPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var totalInFile = file.Count();
            var completedInFile = 0;
            progress.FileStarted(file.Key, completedOverall, totalOverall);

            foreach (var plan in file)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var error = await ExtractTrackAsync(plan, subtitleTimeShift, cancellationToken).ConfigureAwait(false);
                completedInFile++;
                completedOverall++;

                if (error is null)
                {
                    succeeded++;
                    progress.TrackFinished(
                        file.Key, completedInFile, totalInFile, completedOverall, totalOverall);
                }
                else
                {
                    failed++;
                    progress.TrackFailed(
                        file.Key, completedInFile, totalInFile, completedOverall, totalOverall, error);
                }
            }

            progress.FileFinished(file.Key);
        }

        return new ExtractionSummary(succeeded, failed);
    }

    private async Task<string?> ExtractTrackAsync(
        OutputPlan plan,
        TimeSpan subtitleTimeShift,
        CancellationToken cancellationToken)
    {
        var tempPath = plan.FinalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            var result = await _processRunner
                .RunAsync(_ffmpegPath, BuildArguments(plan, tempPath), cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                DeleteTemp(tempPath);
                return $"ffmpeg failed with exit code {result.ExitCode}: {result.StandardError.Trim()}";
            }

            if (!File.Exists(tempPath))
            {
                return $"ffmpeg did not produce an output file for '{plan.FinalPath}'.";
            }

            if (subtitleTimeShift != TimeSpan.Zero &&
                plan.Extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
            {
                SrtTimeShifter.ShiftFile(tempPath, subtitleTimeShift);
            }

            File.Move(tempPath, plan.FinalPath, overwrite: true);
            return null;
        }
        catch (OperationCanceledException)
        {
            DeleteTemp(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            DeleteTemp(tempPath);
            return $"Could not write '{plan.FinalPath}': {ex.Message}";
        }
    }

    private static IReadOnlyList<string> BuildArguments(OutputPlan plan, string tempPath) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel", "error",
        "-y",
        "-i", plan.Track.VideoPath,
        "-map", $"0:{plan.Track.StreamIndex}",
        "-c:s", plan.Mode == SubtitleWriteMode.Copy ? "copy" : "srt",
        "-f", plan.FfmpegFormat,
        tempPath,
    ];

    private static void DeleteTemp(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}

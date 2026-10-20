using SubExtractor.Core.Ffprobe;
using SubExtractor.Core.Models;

namespace SubExtractor.Core.Scanning;

public sealed class ScanService
{
    private readonly ISubtitleProbe _probe;

    public ScanService(ISubtitleProbe probe) => _probe = probe;

    public async Task<ScanResult> ScanAsync(
        string folder,
        IScanProgressSink progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> files;
        try
        {
            files = VideoFileEnumerator.Enumerate(folder);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress.FolderFailed(ex.Message);
            return new ScanResult([], ex.Message);
        }

        var videos = new List<VideoItem>(files.Count);

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = files[index];
            progress.FileStarted(filePath, index, files.Count);

            try
            {
                var tracks = await _probe
                    .ProbeAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);

                videos.Add(new VideoItem(filePath, ProbeStatus.Ok, tracks, null));
                progress.FileFinished(filePath, index + 1, files.Count, tracks.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                videos.Add(new VideoItem(filePath, ProbeStatus.Failed, [], ex.Message));
                progress.FileFailed(filePath, index + 1, files.Count, ex.Message);
            }
        }

        return new ScanResult(videos, null);
    }
}

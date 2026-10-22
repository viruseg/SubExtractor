using Microsoft.UI.Dispatching;
using SubExtractor.Core.Extraction;
using SubExtractor.ViewModels;

namespace SubExtractor.Services;

public sealed class ExtractionProgressSink(ActivityLog log, DispatcherQueue dispatcher)
    : IExtractionProgressSink
{
    private readonly Dictionary<string, LogEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void FileStarted(string videoPath, int completedOverall, int totalOverall) =>
        Post(() =>
        {
            var entry = log.Add($"{NameOf(videoPath)} — 0%");
            _entries[videoPath] = entry;
            SetProgress(
                ExtractionProgress.Fraction(completedOverall, 0, totalOverall), live: false);
        });

    public void TrackProgress(
        string videoPath,
        int completedInFile,
        int totalInFile,
        double currentFraction,
        int completedOverall,
        int totalOverall) =>
        Post(() =>
        {
            UpdateEntry(videoPath, completedInFile + currentFraction, totalInFile, live: true);
            SetProgress(
                ExtractionProgress.Fraction(completedOverall, currentFraction, totalOverall),
                live: true);
        });

    public void TrackFinished(
        string videoPath,
        int completedInFile,
        int totalInFile,
        int completedOverall,
        int totalOverall) =>
        Post(() =>
        {
            UpdateEntry(videoPath, completedInFile, totalInFile, live: false);
            SetProgress(
                ExtractionProgress.Fraction(completedOverall, 0, totalOverall), live: false);
        });

    public void TrackFailed(
        string videoPath,
        int completedInFile,
        int totalInFile,
        int completedOverall,
        int totalOverall,
        string error) =>
        Post(() =>
        {
            UpdateEntry(videoPath, completedInFile, totalInFile, live: false);
            log.Add(error, isError: true);
            SetProgress(
                ExtractionProgress.Fraction(completedOverall, 0, totalOverall), live: false);
        });

    public void FileFinished(string videoPath) =>
        Post(() =>
        {
            var message = $"{NameOf(videoPath)} — 100%";
            if (_entries.TryGetValue(videoPath, out var entry))
            {
                log.Update(entry, message);
            }
            else
            {
                log.Add(message);
            }
        });

    public Task FlushAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(completion.SetResult);
        return completion.Task;
    }

    private void UpdateEntry(string videoPath, double completed, int total, bool live)
    {
        var fraction = total <= 0 ? 0 : Math.Clamp(completed / total, 0, 1);
        var percent = live
            ? ExtractionProgress.LivePercent(fraction)
            : ExtractionProgress.FinishedPercent(completed, total);
        var message = $"{NameOf(videoPath)} — {percent}%";
        if (_entries.TryGetValue(videoPath, out var entry))
        {
            log.Update(entry, message);
        }
        else
        {
            log.Add(message);
        }
    }

    private void Post(Action action) => dispatcher.TryEnqueue(() => action());

    private void SetProgress(double fraction, bool live) =>
        log.SetProgress(live ? Math.Min(fraction * 100.0, 99.0) : fraction * 100.0);

    private static string NameOf(string filePath) =>
        Path.GetFileName(filePath) is { Length: > 0 } name ? name : filePath;
}

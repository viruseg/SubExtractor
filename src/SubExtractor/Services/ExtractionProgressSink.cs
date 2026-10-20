using Microsoft.UI.Dispatching;
using SubExtractor.Core.Extraction;
using SubExtractor.ViewModels;
using SubExtractor.Views;

namespace SubExtractor.Services;

public sealed class ExtractionProgressSink(ProgressWindow progress, DispatcherQueue dispatcher)
    : IExtractionProgressSink
{
    private readonly Dictionary<string, LogEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void FileStarted(string videoPath, int completedOverall, int totalOverall) =>
        Post(() =>
        {
            var entry = progress.AddLog($"{NameOf(videoPath)} — 0%");
            _entries[videoPath] = entry;
            SetOverall(completedOverall, totalOverall);
        });

    public void TrackFinished(
        string videoPath,
        int completedInFile,
        int totalInFile,
        int completedOverall,
        int totalOverall) =>
        Post(() =>
        {
            UpdateEntry(videoPath, completedInFile, totalInFile);
            SetOverall(completedOverall, totalOverall);
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
            UpdateEntry(videoPath, completedInFile, totalInFile);
            progress.AddLog(error, isError: true);
            SetOverall(completedOverall, totalOverall);
        });

    public void FileFinished(string videoPath) =>
        Post(() =>
        {
            var message = $"{NameOf(videoPath)} — 100%";
            if (_entries.TryGetValue(videoPath, out var entry))
            {
                progress.UpdateLog(entry, message);
            }
            else
            {
                progress.AddLog(message);
            }
        });

    public Task FlushAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(completion.SetResult);
        return completion.Task;
    }

    private void UpdateEntry(string videoPath, int completedInFile, int totalInFile)
    {
        var message = $"{NameOf(videoPath)} — {Percent(completedInFile, totalInFile)}";
        if (_entries.TryGetValue(videoPath, out var entry))
        {
            progress.UpdateLog(entry, message);
        }
        else
        {
            progress.AddLog(message);
        }
    }

    private void Post(Action action) => dispatcher.TryEnqueue(() => action());

    private void SetOverall(int completed, int total) =>
        progress.SetOverall(total == 0 ? 0 : completed * 100.0 / total);

    private static string Percent(int completed, int total) =>
        $"{(total == 0 ? 0 : completed * 100.0 / total):0}%";

    private static string NameOf(string filePath) =>
        Path.GetFileName(filePath) is { Length: > 0 } name ? name : filePath;
}

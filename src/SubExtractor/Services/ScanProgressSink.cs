using Microsoft.UI.Dispatching;
using SubExtractor.Core.Scanning;
using SubExtractor.ViewModels;
using SubExtractor.Views;

namespace SubExtractor.Services;

public sealed class ScanProgressSink(ProgressWindow progress, DispatcherQueue dispatcher) : IScanProgressSink
{
    private readonly Dictionary<string, LogEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void FileStarted(string filePath, int completedFiles, int totalFiles) =>
        Post(() =>
        {
            var entry = progress.AddLog($"{NameOf(filePath)} — 0%");
            _entries[filePath] = entry;
            SetOverall(completedFiles, totalFiles);
        });

    public void FileFinished(string filePath, int completedFiles, int totalFiles, int trackCount) =>
        Post(() =>
        {
            var message = $"{NameOf(filePath)} — 100% ({trackCount} subtitles)";
            if (_entries.TryGetValue(filePath, out var entry))
            {
                progress.UpdateLog(entry, message);
            }
            else
            {
                progress.AddLog(message);
            }

            SetOverall(completedFiles, totalFiles);
        });

    public void FileFailed(string filePath, int completedFiles, int totalFiles, string error) =>
        Post(() =>
        {
            var message = $"{NameOf(filePath)} — failed: {error}";
            if (_entries.TryGetValue(filePath, out var entry))
            {
                progress.UpdateLog(entry, message, isError: true);
            }
            else
            {
                progress.AddLog(message, isError: true);
            }

            SetOverall(completedFiles, totalFiles);
        });

    public void FolderFailed(string error) =>
        Post(() => progress.AddLog($"Folder failed: {error}", isError: true));

    public Task FlushAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(completion.SetResult);
        return completion.Task;
    }

    private void Post(Action action) => dispatcher.TryEnqueue(() => action());

    private void SetOverall(int completedFiles, int totalFiles) =>
        progress.SetOverall(totalFiles == 0 ? 0 : completedFiles * 100.0 / totalFiles);

    private static string NameOf(string filePath) =>
        Path.GetFileName(filePath) is { Length: > 0 } name ? name : filePath;
}

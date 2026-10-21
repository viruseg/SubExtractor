using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SubExtractor.ViewModels;

namespace SubExtractor.Services;

public sealed class ActivityLog : INotifyPropertyChanged
{
    private string _title = "Activity log";
    private string _lastMessage = string.Empty;
    private bool _lastIsError;
    private bool _isRunning;
    private bool _isCancelRequested;
    private double? _progress;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CancelRequested;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    public string LastMessage
    {
        get => _lastMessage;
        private set => Set(ref _lastMessage, value);
    }

    public bool LastIsError
    {
        get => _lastIsError;
        private set => Set(ref _lastIsError, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => Set(ref _isRunning, value);
    }

    public bool IsCancelRequested
    {
        get => _isCancelRequested;
        private set => Set(ref _isCancelRequested, value);
    }

    public double? Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public void BeginOperation(string title)
    {
        if (Entries.Count > 0)
        {
            Entries.Add(new LogEntry(
                DateTime.Now.ToString("HH:mm:ss"), string.Empty, isSeparator: true));
        }

        Title = title;
        IsCancelRequested = false;
        Progress = 0;
        IsRunning = true;
    }

    public LogEntry Add(string message, bool isError = false)
    {
        var entry = new LogEntry(DateTime.Now.ToString("HH:mm:ss"), message, isError);
        Entries.Add(entry);
        LastMessage = message;
        LastIsError = isError;
        return entry;
    }

    public void Update(LogEntry entry, string message, bool isError = false)
    {
        entry.Message = message;
        entry.IsError = isError;

        if (Entries.Count > 0 && ReferenceEquals(Entries[^1], entry))
        {
            LastMessage = message;
            LastIsError = isError;
        }
    }

    public void SetProgress(double? percent) => Progress = percent;

    public void EndOperation() => IsRunning = false;

    public void RequestCancel()
    {
        if (!IsRunning || IsCancelRequested)
        {
            return;
        }

        IsCancelRequested = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

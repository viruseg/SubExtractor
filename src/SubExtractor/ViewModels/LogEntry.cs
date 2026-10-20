using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SubExtractor.ViewModels;

public sealed class LogEntry : INotifyPropertyChanged
{
    private string _message;
    private bool _isError;

    public LogEntry(string timestamp, string message, bool isError = false)
    {
        Timestamp = timestamp;
        _message = message;
        _isError = isError;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Timestamp { get; }

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value)
            {
                return;
            }

            _message = value;
            OnPropertyChanged();
        }
    }

    public bool IsError
    {
        get => _isError;
        set
        {
            if (_isError == value)
            {
                return;
            }

            _isError = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Models;

namespace SubExtractor.ViewModels;

public enum TableCellKind
{
    File,
    Subtitle,
}

public sealed class TableCellViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public TableCellViewModel(
        TableCellKind kind,
        string fileName,
        string? statusText,
        IReadOnlyList<SubtitleTrack> tracks,
        double width)
    {
        Kind = kind;
        FileName = fileName;
        StatusText = statusText;
        Tracks = tracks;
        Width = width;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SelectionChanged;

    public TableCellKind Kind { get; }

    public string FileName { get; }

    public string? StatusText { get; }

    public IReadOnlyList<SubtitleTrack> Tracks { get; }

    public double Width { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Visibility FileNameVisibility =>
        Kind == TableCellKind.File ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StatusVisibility =>
        string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CheckBoxVisibility =>
        Kind == TableCellKind.Subtitle && Tracks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

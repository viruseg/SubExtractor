using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.ViewModels;

public enum TableCellKind
{
    File,
    Subtitle,
}

public enum FileStatus
{
    None,
    NoSubtitles,
    ProbeFailed,
}

public sealed class TableCellViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private double _width;

    public TableCellViewModel(
        TableCellKind kind,
        string fileName,
        FileStatus status,
        IReadOnlyList<SubtitleTrack> tracks,
        double width)
    {
        Kind = kind;
        FileName = fileName;
        Status = status;
        Tracks = tracks;
        _width = width;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SelectionChanged;

    public TableCellKind Kind { get; }

    public string FileName { get; }

    public FileStatus Status { get; }

    public IReadOnlyList<SubtitleTrack> Tracks { get; }

    public TableRowViewModel? Row { get; internal set; }

    public string? ColumnLabel { get; internal set; }

    public double Width
    {
        get => _width;
        set
        {
            if (Math.Abs(_width - value) < 0.5)
            {
                return;
            }

            _width = value;
            PropertyChanged?.Invoke(this, new(nameof(Width)));
        }
    }

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
            PropertyChanged?.Invoke(this, new(nameof(CheckedIndicatorVisibility)));
            PropertyChanged?.Invoke(this, new(nameof(UncheckedIndicatorVisibility)));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasTrack => Tracks.Count > 0;

    public string FormatLabel =>
        Tracks.Count == 0
            ? string.Empty
            : string.Join(
                "/",
                Tracks
                    .Select(track => SubtitleFormats.For(track.CodecName).Extension.TrimStart('.'))
                    .Distinct());

    public Visibility CheckedIndicatorVisibility =>
        HasTrack && IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UncheckedIndicatorVisibility =>
        HasTrack && !IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyMarkVisibility =>
        HasTrack ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FormatChipVisibility =>
        HasTrack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoSubtitlesVisibility =>
        Status == FileStatus.NoSubtitles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProbeFailedVisibility =>
        Status == FileStatus.ProbeFailed ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText =>
        Status switch
        {
            FileStatus.NoSubtitles => "No subtitles",
            FileStatus.ProbeFailed => "Probe failed",
            _ => string.Empty,
        };

    public Visibility TrackCellVisibility =>
        Kind == TableCellKind.Subtitle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FileCellVisibility =>
        Kind == TableCellKind.File ? Visibility.Visible : Visibility.Collapsed;

    public string AutomationName
    {
        get
        {
            var column = ColumnLabel ?? string.Empty;
            var file = Row?.FileCell.FileName ?? string.Empty;

            if (string.IsNullOrEmpty(column))
            {
                return file;
            }

            return string.IsNullOrEmpty(file) ? column : $"{column}, {file}";
        }
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public const double MinFileColumnWidth = 260;
    public const double GutterWidth = 3;

    private bool _canExtract;
    private bool _hasResults;
    private string _emptyStateText = "Choose a folder to scan.";
    private double _fileColumnWidth = 320;
    private double _fileContentWidth;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TableColumnViewModel> Columns { get; } = [];

    public ObservableCollection<TableRowViewModel> Rows { get; } = [];

    public double SubtitleColumnsWidth =>
        Columns
            .Where(column => column.Kind == TableCellKind.Subtitle)
            .Sum(column => column.Width);

    public bool CanExtract
    {
        get => _canExtract;
        private set
        {
            if (_canExtract == value)
            {
                return;
            }

            _canExtract = value;
            OnPropertyChanged();
        }
    }

    public bool HasResults
    {
        get => _hasResults;
        private set
        {
            if (_hasResults == value)
            {
                return;
            }

            _hasResults = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        set
        {
            if (_emptyStateText == value)
            {
                return;
            }

            _emptyStateText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
    }

    public Visibility EmptyStateVisibility =>
        HasResults || string.IsNullOrEmpty(EmptyStateText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public IReadOnlyList<SubtitleTrack> SelectedTracks =>
        Columns
            .SelectMany(column => column.Cells)
            .Where(cell => cell.IsSelected)
            .SelectMany(cell => cell.Tracks)
            .ToList();

    public void SetVideos(IReadOnlyList<VideoItem> videos)
    {
        foreach (var column in Columns)
        {
            column.SelectionChanged -= Column_SelectionChanged;
        }

        Columns.Clear();
        Rows.Clear();

        var fileColumn = new TableColumnViewModel(
            TableCellKind.File, string.Empty, "File", string.Empty, _fileColumnWidth);
        Columns.Add(fileColumn);

        var subtitleColumns = ColumnBuilder
            .Build(videos)
            .Select(definition => new TableColumnViewModel(
                TableCellKind.Subtitle,
                definition.Key,
                definition.DisplayTitle,
                definition.DisplayLanguage,
                ColumnWidthPolicy.Measure(definition.DisplayTitle, definition.DisplayLanguage)))
            .ToList();

        foreach (var column in subtitleColumns)
        {
            column.SelectionChanged += Column_SelectionChanged;
            Columns.Add(column);
        }

        foreach (var video in videos)
        {
            var status = video.Status == ProbeStatus.Failed
                ? FileStatus.ProbeFailed
                : video.Tracks.Count == 0
                    ? FileStatus.NoSubtitles
                    : FileStatus.None;

            var cells = new List<TableCellViewModel>(subtitleColumns.Count + 1)
            {
                new(TableCellKind.File, Path.GetFileName(video.FilePath), status, [], _fileColumnWidth),
            };

            foreach (var column in subtitleColumns)
            {
                var tracks = video.Tracks
                    .Where(track =>
                        TrackKey.Build(track.Title, track.Language) == column.Key)
                    .ToList();

                var cell = new TableCellViewModel(
                    TableCellKind.Subtitle, string.Empty, FileStatus.None, tracks, column.Width);
                cells.Add(cell);

                if (tracks.Count > 0)
                {
                    column.AddCell(cell);
                }
            }

            Rows.Add(new TableRowViewModel(cells));
        }

        HasResults = Rows.Count > 0;
        _fileContentWidth = Rows.Count == 0
            ? 0
            : Rows.Max(row =>
                ColumnWidthPolicy.MeasureFileCell(row.FileCell.FileName, row.FileCell.StatusText));
        UpdateCanExtract();
    }

    public void SetFileContentWidth(double width)
    {
        if (Math.Abs(width - _fileContentWidth) < 0.5)
        {
            return;
        }

        _fileContentWidth = width;
    }

    public void SetFileColumnWidth(double viewportWidth)
    {
        if (viewportWidth <= 0)
        {
            return;
        }

        var upper = Math.Max(viewportWidth - GutterWidth - SubtitleColumnsWidth, MinFileColumnWidth);
        var width = Math.Min(Math.Max(_fileContentWidth, MinFileColumnWidth), upper);
        if (Math.Abs(width - _fileColumnWidth) < 0.5)
        {
            return;
        }

        _fileColumnWidth = width;

        foreach (var column in Columns.Where(column => column.Kind == TableCellKind.File))
        {
            column.Width = width;
        }

        foreach (var row in Rows)
        {
            row.FileCell.Width = width;
        }
    }

    private void Column_SelectionChanged(object? sender, EventArgs e) => UpdateCanExtract();

    private void UpdateCanExtract() =>
        CanExtract = Columns.Any(column => column.Cells.Any(cell => cell.IsSelected));

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

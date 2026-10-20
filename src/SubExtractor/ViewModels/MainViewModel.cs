using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;
using SubExtractor.Core.Models;

namespace SubExtractor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const double FileColumnWidth = 280;
    private const double SubtitleColumnWidth = 180;

    private bool _canExtract;
    private bool _hasResults;
    private string _emptyStateText = "Choose a folder to scan.";
    private string _statusText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TableColumnViewModel> Columns { get; } = [];

    public ObservableCollection<TableRowViewModel> Rows { get; } = [];

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

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
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
            TableCellKind.File, string.Empty, "File", FileColumnWidth);
        Columns.Add(fileColumn);

        var subtitleColumns = ColumnBuilder
            .Build(videos)
            .Select(definition => new TableColumnViewModel(
                TableCellKind.Subtitle,
                definition.Key,
                definition.HeaderLabel,
                SubtitleColumnWidth))
            .ToList();

        foreach (var column in subtitleColumns)
        {
            column.SelectionChanged += Column_SelectionChanged;
            Columns.Add(column);
        }

        foreach (var video in videos)
        {
            var status = video.Status == ProbeStatus.Failed
                ? "Probe failed"
                : video.Tracks.Count == 0
                    ? "No subtitles"
                    : null;

            var cells = new List<TableCellViewModel>(subtitleColumns.Count + 1)
            {
                new(TableCellKind.File, Path.GetFileName(video.FilePath), status, [], FileColumnWidth),
            };

            foreach (var column in subtitleColumns)
            {
                var tracks = video.Tracks
                    .Where(track =>
                        TrackKey.Build(track.Title, track.Language) == column.Key)
                    .ToList();

                var cell = new TableCellViewModel(
                    TableCellKind.Subtitle, string.Empty, null, tracks, SubtitleColumnWidth);
                cells.Add(cell);

                if (tracks.Count > 0)
                {
                    column.AddCell(cell);
                }
            }

            Rows.Add(new TableRowViewModel(cells));
        }

        HasResults = Rows.Count > 0;
        UpdateCanExtract();
    }

    private void Column_SelectionChanged(object? sender, EventArgs e) => UpdateCanExtract();

    private void UpdateCanExtract() =>
        CanExtract = Columns.Any(column => column.Cells.Any(cell => cell.IsSelected));

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

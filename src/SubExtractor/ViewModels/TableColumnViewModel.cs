using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;

namespace SubExtractor.ViewModels;

public sealed class TableColumnViewModel : INotifyPropertyChanged
{
    private bool? _selectionState = false;
    private double _width;

    public TableColumnViewModel(
        TableCellKind kind,
        string key,
        string title,
        string language,
        double width)
    {
        Kind = kind;
        Key = key;
        Title = title;
        Language = language;
        _width = width;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SelectionChanged;

    public TableCellKind Kind { get; }

    public string Key { get; }

    public string Title { get; }

    public string Language { get; }

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

    public ObservableCollection<TableCellViewModel> Cells { get; } = [];

    public string HeaderLabel =>
        string.IsNullOrEmpty(Title)
            ? Language.ToUpperInvariant()
            : string.IsNullOrEmpty(Language)
                ? Title
                : $"{Title} ({Language.ToUpperInvariant()})";

    public string PrimaryLabel =>
        string.IsNullOrEmpty(Title) ? Language.ToUpperInvariant() : Title;

    public string SecondaryLabel =>
        string.IsNullOrEmpty(Title) ? string.Empty : Language.ToUpperInvariant();

    public Visibility SecondaryLabelVisibility =>
        string.IsNullOrEmpty(SecondaryLabel) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HeaderButtonVisibility =>
        Kind == TableCellKind.Subtitle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HeaderLabelVisibility =>
        Kind == TableCellKind.File ? Visibility.Visible : Visibility.Collapsed;

    public bool? SelectionState
    {
        get => _selectionState;
        private set
        {
            if (_selectionState == value)
            {
                return;
            }

            _selectionState = value;
            PropertyChanged?.Invoke(this, new(nameof(SelectionState)));
            PropertyChanged?.Invoke(this, new(nameof(IsFullySelected)));
            PropertyChanged?.Invoke(this, new(nameof(IsPartiallySelected)));
            PropertyChanged?.Invoke(this, new(nameof(AccentBarVisibility)));
            PropertyChanged?.Invoke(this, new(nameof(AccentBarOpacity)));
        }
    }

    public bool IsFullySelected => SelectionState == true;

    public bool IsPartiallySelected => SelectionState == null;

    public Visibility AccentBarVisibility =>
        IsFullySelected || IsPartiallySelected ? Visibility.Visible : Visibility.Collapsed;

    public double AccentBarOpacity => IsFullySelected ? 1.0 : 0.55;

    public void AddCell(TableCellViewModel cell)
    {
        cell.SelectionChanged += Cell_SelectionChanged;
        cell.ColumnLabel = HeaderLabel;
        Cells.Add(cell);
        UpdateSelectionState();
    }

    public void ToggleAll()
    {
        var select = SelectionRules.ShouldSelectAll(Cells.Select(cell => cell.IsSelected));

        foreach (var cell in Cells)
        {
            cell.IsSelected = select;
        }

        UpdateSelectionState();
    }

    private void Cell_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateSelectionState();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionState() =>
        SelectionState = SelectionRules.ComputeState(Cells.Select(cell => cell.IsSelected));
}

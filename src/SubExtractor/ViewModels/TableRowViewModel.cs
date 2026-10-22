using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;

namespace SubExtractor.ViewModels;

public sealed class TableRowViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<TableCellViewModel> _selectableCells;
    private bool _isFullySelected;

    public TableRowViewModel(IReadOnlyList<TableCellViewModel> cells)
    {
        Cells = cells;
        FileCell = cells.First(cell => cell.Kind == TableCellKind.File);
        _selectableCells = cells
            .Where(cell => cell.Kind == TableCellKind.Subtitle && cell.HasTrack)
            .ToList();

        foreach (var cell in cells)
        {
            cell.Row = this;
            cell.SelectionChanged += Cell_SelectionChanged;
        }

        UpdateSelectionState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<TableCellViewModel> Cells { get; }

    public TableCellViewModel FileCell { get; }

    public bool IsFullySelected
    {
        get => _isFullySelected;
        private set
        {
            if (_isFullySelected == value)
            {
                return;
            }

            _isFullySelected = value;
            PropertyChanged?.Invoke(this, new(nameof(IsFullySelected)));
            PropertyChanged?.Invoke(this, new(nameof(SelectionBarVisibility)));
        }
    }

    public Visibility SelectionBarVisibility =>
        IsFullySelected ? Visibility.Visible : Visibility.Collapsed;

    public void ToggleAll()
    {
        var select = SelectionRules.ShouldSelectAll(_selectableCells.Select(cell => cell.IsSelected));

        foreach (var cell in _selectableCells)
        {
            cell.IsSelected = select;
        }
    }

    private void Cell_SelectionChanged(object? sender, EventArgs e) => UpdateSelectionState();

    private void UpdateSelectionState() =>
        IsFullySelected = _selectableCells.Count > 0 && _selectableCells.All(cell => cell.IsSelected);
}

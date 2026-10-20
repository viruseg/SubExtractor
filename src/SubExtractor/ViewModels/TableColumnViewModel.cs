using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using SubExtractor.Core.Formatting;

namespace SubExtractor.ViewModels;

public sealed class TableColumnViewModel : INotifyPropertyChanged
{
    private bool? _selectionState = false;

    public TableColumnViewModel(TableCellKind kind, string key, string headerLabel, double width)
    {
        Kind = kind;
        Key = key;
        HeaderLabel = headerLabel;
        Width = width;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SelectionChanged;

    public TableCellKind Kind { get; }

    public string Key { get; }

    public string HeaderLabel { get; }

    public double Width { get; }

    public ObservableCollection<TableCellViewModel> Cells { get; } = [];

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
        }
    }

    public bool IsSelectable => Kind == TableCellKind.Subtitle && Cells.Count > 0;

    public Visibility CheckBoxVisibility =>
        Kind == TableCellKind.Subtitle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LabelVisibility => Visibility.Visible;

    public void AddCell(TableCellViewModel cell)
    {
        cell.SelectionChanged += Cell_SelectionChanged;
        Cells.Add(cell);
        PropertyChanged?.Invoke(this, new(nameof(IsSelectable)));
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

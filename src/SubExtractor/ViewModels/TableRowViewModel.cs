namespace SubExtractor.ViewModels;

public sealed class TableRowViewModel(IReadOnlyList<TableCellViewModel> cells)
{
    public IReadOnlyList<TableCellViewModel> Cells { get; } = cells;
}

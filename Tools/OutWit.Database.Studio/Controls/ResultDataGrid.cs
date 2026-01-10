using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using OutWit.Common.MVVM.Attributes;
using OutWit.Common.MVVM.Table;
using OutWit.Database.Studio.Converters;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// Custom DataGrid that displays TableView data.
/// Automatically generates columns from TableView.HeaderRow.
/// Supports NULL value display and selection tracking.
/// </summary>
public partial class ResultDataGrid : DataGrid
{
    #region Static

    static ResultDataGrid()
    {
        HeaderRowProperty.Changed.AddClassHandler<ResultDataGrid>((grid, e) => grid.OnHeaderRowChanged(e));
        ResultPageProperty.Changed.AddClassHandler<ResultDataGrid>((grid, e) => grid.OnResultPageChanged(e));
    }

    #endregion

    #region Constructors

    public ResultDataGrid()
    {
        AutoGenerateColumns = false;
        IsReadOnly = true;
        GridLinesVisibility = DataGridGridLinesVisibility.All;
        CanUserResizeColumns = true;
        CanUserReorderColumns = true;
        CanUserSortColumns = true;
        SelectionMode = DataGridSelectionMode.Extended;

        SelectionChanged += OnSelectionChanged;
    }

    #endregion

    #region Functions

    private void OnHeaderRowChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var headerRow = e.NewValue as TableViewRow;
        
        Columns.Clear();
        
        if (headerRow == null || headerRow.Values.Length == 0)
            return;

        // Generate columns from header row
        for (var i = 0; i < headerRow.Values.Length; i++)
        {
            var cell = headerRow.Values[i];
            var columnName = cell.Text ?? $"Column{i}";
            
            var dataGridColumn = new DataGridTextColumn
            {
                Header = columnName,
                Binding = new Binding($"[{i}].Text") { Converter = new NullValueConverter() },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 50,
                Tag = i
            };

            Columns.Add(dataGridColumn);
        }
    }

    private void OnResultPageChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var page = e.NewValue as TableViewPage;
        ItemsSource = page?.Rows;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedRows = new ObservableCollection<TableViewRow>();
        
        foreach (var item in SelectedItems)
        {
            if (item is TableViewRow row)
            {
                selectedRows.Add(row);
            }
        }

        SelectedRows = selectedRows;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The header row with column names.
    /// </summary>
    [StyledProperty]
    public TableViewRow? HeaderRow { get; set; }

    /// <summary>
    /// The page of results to display.
    /// </summary>
    [StyledProperty]
    public TableViewPage? ResultPage { get; set; }

    /// <summary>
    /// The currently selected rows.
    /// </summary>
    [StyledProperty]
    public ObservableCollection<TableViewRow>? SelectedRows { get; set; }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    #endregion
}

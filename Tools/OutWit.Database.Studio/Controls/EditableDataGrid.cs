using System.Data;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using OutWit.Common.MVVM.Attributes;
using OutWit.Database.Studio.Converters;
using OutWit.Database.Studio.Models;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// Custom DataGrid for editing table data.
/// Supports inline editing, type validation, and NULL handling.
/// </summary>
public partial class EditableDataGrid : DataGridBase
{
    #region Static

    static EditableDataGrid()
    {
        ColumnInfosProperty.Changed.AddClassHandler<EditableDataGrid>((grid, e) => grid.OnColumnInfosChanged(e));
    }

    #endregion

    #region Constructors

    public EditableDataGrid()
    {
        IsReadOnly = false;
        SelectionMode = DataGridSelectionMode.Single;
        
        SelectionChanged += OnSelectionChanged;
        CellEditEnding += OnCellEditEnding;
    }

    #endregion

    #region Functions

    protected override DataGridTextColumn CreateColumn(DataColumn dataColumn, int ordinal, string className)
    {
        var columnInfo = ColumnInfos?.FirstOrDefault(c => c.Name == dataColumn.ColumnName);
        var columnCount = ResultView?.Table?.Columns.Count ?? 1;

        return new DataGridTextColumn
        {
            Header = dataColumn.ColumnName,
            Binding = new Binding($"Row.ItemArray[{ordinal}]")
            {
                Converter = m_valueConverter,
                Mode = BindingMode.OneWay // Display only, editing handled manually
            },
            Width = columnCount <= 5
                ? new DataGridLength(1, DataGridLengthUnitType.Star)
                : new DataGridLength(120, DataGridLengthUnitType.Pixel),
            MinWidth = 60,
            MaxWidth = 400,
            CanUserSort = true,
            IsReadOnly = columnInfo?.IsPrimaryKey == true || columnInfo?.IsAutoIncrement == true,
            Tag = ordinal
        };
    }

    protected override string GetColumnClassName(int ordinal) => $"edit-col-{ordinal}";

    private void OnColumnInfosChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (ResultView != null)
            RebuildColumns();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetValue(SelectedRowViewProperty, SelectedItem as DataRowView);
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (e.Row.DataContext is not DataRowView rowView)
            return;

        if (e.EditingElement is not TextBox textBox)
            return;

        var columnIndex = e.Column.DisplayIndex;
        if (columnIndex < 0 || ResultView?.Table == null || columnIndex >= ResultView.Table.Columns.Count)
            return;

        var column = ResultView.Table.Columns[columnIndex];
        var columnInfo = ColumnInfos?.FirstOrDefault(c => c.Name == column.ColumnName);
        var newText = textBox.Text ?? string.Empty;

        try
        {
            object? newValue;

            // Handle NULL input
            if (string.IsNullOrEmpty(newText) ||
                newText.Equals(SqlValueConverter.NULL_DISPLAY_TEXT, StringComparison.OrdinalIgnoreCase))
            {
                if (columnInfo?.IsNullable == true)
                {
                    newValue = DBNull.Value;
                }
                else
                {
                    // Non-nullable column - cancel edit
                    e.Cancel = true;
                    return;
                }
            }
            else
            {
                // Use SqlValueParser for type-safe conversion based on WitSqlType
                newValue = SqlValueParser.Parse(newText, column.DataType);
            }

            // Apply the value directly to the DataRow
            rowView.Row[columnIndex] = newValue;

            // Execute command if bound
            if (CellEditedCommand?.CanExecute(rowView) == true)
            {
                CellEditedCommand.Execute(rowView);
            }
        }
        catch
        {
            // Conversion failed - cancel edit
            e.Cancel = true;
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Column information for validation and editing rules.
    /// </summary>
    [StyledProperty]
    public IList<ColumnInfo>? ColumnInfos { get; set; }

    /// <summary>
    /// The currently selected row.
    /// </summary>
    [StyledProperty]
    public DataRowView? SelectedRowView { get; set; }

    /// <summary>
    /// Command executed when a cell is edited. Parameter is DataRowView.
    /// </summary>
    [StyledProperty]
    public ICommand? CellEditedCommand { get; set; }

    #endregion
}
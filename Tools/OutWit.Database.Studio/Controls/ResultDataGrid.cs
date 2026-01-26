using System.Collections;
using System.Data;
using Avalonia;
using Avalonia.Controls;
using OutWit.Common.MVVM.Attributes;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// Custom DataGrid that displays DataView results in read-only mode.
/// Supports NULL value display, automatic column generation, and multi-row selection.
/// </summary>
public partial class ResultDataGrid : DataGridBase
{
    #region Constructors

    public ResultDataGrid()
    {
        IsReadOnly = true;
        SelectionMode = DataGridSelectionMode.Extended;
        
        SelectionChanged += OnSelectionChanged;
    }

    #endregion

    #region Functions

    protected override string GetColumnClassName(int ordinal) => $"result-col-{ordinal}";

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = new List<DataRowView>();

        foreach (var item in SelectedItems)
        {
            if (item is DataRowView rowView)
            {
                selected.Add(rowView);
            }
        }

        SetValue(SelectedRowsProperty, selected);
    }

    #endregion

    #region Properties

    /// <summary>
    /// The currently selected rows.
    /// </summary>
    [StyledProperty]
    public IList? SelectedRows { get; set; }

    #endregion
}
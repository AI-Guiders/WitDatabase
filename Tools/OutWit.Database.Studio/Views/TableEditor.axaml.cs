using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OutWit.Database.Studio.Controls;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views;

/// <summary>
/// Table editor view for inline editing of table data.
/// </summary>
public partial class TableEditor : UserControl
{
    #region Constructors

    public TableEditor()
    {
        AvaloniaXamlLoader.Load(this);
        InitEvents();
    }

    #endregion

    #region Initialization

    private void InitEvents()
    {
        var dataGrid = this.FindControl<EditableDataGrid>("DataGrid");
        if (dataGrid != null)
        {
            dataGrid.CellEdited += OnCellEdited;
        }
    }

    #endregion

    #region Event Handlers

    private void OnCellEdited(object? sender, CellEditedEventArgs e)
    {
        if (DataContext is ApplicationViewModel appVm)
        {
            appVm.TableEditorVm.OnCellEdited(e.RowView);
        }
    }

    #endregion
}

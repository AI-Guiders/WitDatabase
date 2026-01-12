using System.Data;

namespace OutWit.Database.Studio.Models;

/// <summary>
/// Event args for cell edited event.
/// </summary>
public class CellEditedEventArgs : EventArgs
{
    #region Constructors

    public CellEditedEventArgs(DataRowView rowView, string columnName, object? newValue)
    {
        RowView = rowView;
        ColumnName = columnName;
        NewValue = newValue;
    }

    #endregion

    #region Proeprties

    public DataRowView RowView { get; }
    public string ColumnName { get; }
    public object? NewValue { get; }

    #endregion
}
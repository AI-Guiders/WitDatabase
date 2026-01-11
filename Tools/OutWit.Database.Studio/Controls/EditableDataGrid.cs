using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using OutWit.Database.Studio.Converters;
using OutWit.Database.Studio.Models;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// Custom DataGrid for editing table data.
/// Supports inline editing, type validation, and NULL handling.
/// </summary>
public class EditableDataGrid : DataGrid
{
    #region Styled Properties

    public static readonly StyledProperty<DataView?> ResultViewProperty =
        AvaloniaProperty.Register<EditableDataGrid, DataView?>(nameof(ResultView));

    public static readonly StyledProperty<IList<ColumnInfo>?> ColumnInfosProperty =
        AvaloniaProperty.Register<EditableDataGrid, IList<ColumnInfo>?>(nameof(ColumnInfos));

    public static readonly StyledProperty<DataRowView?> SelectedRowViewProperty =
        AvaloniaProperty.Register<EditableDataGrid, DataRowView?>(nameof(SelectedRowView));

    #endregion

    #region Static

    static EditableDataGrid()
    {
        ResultViewProperty.Changed.AddClassHandler<EditableDataGrid>((grid, e) => grid.OnResultViewChanged(e));
        ColumnInfosProperty.Changed.AddClassHandler<EditableDataGrid>((grid, e) => grid.OnColumnInfosChanged(e));
    }

    #endregion

    #region Fields

    private readonly List<IStyle> m_dynamicStyles = [];
    private readonly SqlValueBrushConverter m_valueBrushConverter = new();

    #endregion

    #region Constructors

    public EditableDataGrid()
    {
        InitDefaults();
        InitEvents();
    }

    #endregion

    #region Initialization

    private void InitDefaults()
    {
        AutoGenerateColumns = false;
        IsReadOnly = false;
        GridLinesVisibility = DataGridGridLinesVisibility.All;
        CanUserResizeColumns = true;
        CanUserReorderColumns = true;
        CanUserSortColumns = true;
        SelectionMode = DataGridSelectionMode.Single;
    }

    private void InitEvents()
    {
        SelectionChanged += OnSelectionChanged;
        CellEditEnding += OnCellEditEnding;
    }

    #endregion

    #region Functions

    private void ClearDynamicStyles()
    {
        foreach (var s in m_dynamicStyles)
            Styles.Remove(s);

        m_dynamicStyles.Clear();
    }

    private void RebuildColumns()
    {
        Columns.Clear();
        ClearDynamicStyles();

        if (ResultView?.Table == null)
        {
            ItemsSource = null;
            return;
        }

        var columnCount = ResultView.Table.Columns.Count;

        foreach (DataColumn col in ResultView.Table.Columns)
        {
            var ordinal = col.Ordinal;
            var columnName = col.ColumnName;
            var className = $"edit-col-{ordinal}";
            var columnInfo = ColumnInfos?.FirstOrDefault(c => c.Name == columnName);

            // Use Row.ItemArray[ordinal] for display (read-only binding)
            // Editing is handled manually in OnCellEditEnding
            var dataGridColumn = new DataGridTextColumn
            {
                Header = columnName,
                Binding = new Binding($"Row.ItemArray[{ordinal}]")
                {
                    Converter = new SqlValueConverter(),
                    Mode = BindingMode.OneWay // Display only, editing handled manually
                },
                // Use SizeToCells for reasonable default width, then Star for remaining space
                Width = columnCount <= 5 
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(120, DataGridLengthUnitType.Pixel),
                MinWidth = 60,
                MaxWidth = 400,
                CanUserSort = true,
                IsReadOnly = columnInfo?.IsPrimaryKey == true || columnInfo?.IsAutoIncrement == true,
                Tag = ordinal
            };

            dataGridColumn.CellStyleClasses.Add(className);
            Columns.Add(dataGridColumn);

            // Apply cell styles for NULL values
            var cellStyle = new Style(x => x.OfType<DataGridCell>().Class(className));
            var foregroundBinding = new Binding($"Row.ItemArray[{ordinal}]")
            {
                Converter = m_valueBrushConverter,
            };
            cellStyle.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, foregroundBinding));
            Styles.Add(cellStyle);
            m_dynamicStyles.Add(cellStyle);
        }

        ItemsSource = ResultView;
    }

    private void OnResultViewChanged(AvaloniaPropertyChangedEventArgs e)
    {
        RebuildColumns();
    }

    private void OnColumnInfosChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (ResultView != null)
            RebuildColumns();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedItem is DataRowView rowView)
        {
            SetValue(SelectedRowViewProperty, rowView);
        }
        else
        {
            SetValue(SelectedRowViewProperty, null);
        }
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
                // Convert the text to the appropriate type
                newValue = ConvertValue(newText, column.DataType);
            }

            // Apply the value directly to the DataRow
            rowView.Row[columnIndex] = newValue;

            // Notify that cell was edited
            CellEdited?.Invoke(this, new CellEditedEventArgs(rowView, column.ColumnName, newValue));
        }
        catch
        {
            // Conversion failed - cancel edit
            e.Cancel = true;
        }
    }

    private static object? ConvertValue(string text, Type targetType)
    {
        if (string.IsNullOrEmpty(text))
            return DBNull.Value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
            return text;

        if (underlyingType == typeof(int))
            return int.Parse(text);

        if (underlyingType == typeof(long))
            return long.Parse(text);

        if (underlyingType == typeof(short))
            return short.Parse(text);

        if (underlyingType == typeof(byte))
            return byte.Parse(text);

        if (underlyingType == typeof(decimal))
            return decimal.Parse(text);

        if (underlyingType == typeof(double))
            return double.Parse(text);

        if (underlyingType == typeof(float))
            return float.Parse(text);

        if (underlyingType == typeof(bool))
            return bool.Parse(text);

        if (underlyingType == typeof(DateTime))
            return DateTime.Parse(text);

        if (underlyingType == typeof(DateOnly))
            return DateOnly.Parse(text);

        if (underlyingType == typeof(TimeOnly))
            return TimeOnly.Parse(text);

        if (underlyingType == typeof(Guid))
            return Guid.Parse(text);

        // Fallback: use Convert.ChangeType
        return Convert.ChangeType(text, underlyingType);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a cell value is edited.
    /// </summary>
    public event EventHandler<CellEditedEventArgs>? CellEdited;

    #endregion

    #region Properties

    /// <summary>
    /// The DataView to display and edit.
    /// </summary>
    public DataView? ResultView
    {
        get => GetValue(ResultViewProperty);
        set => SetValue(ResultViewProperty, value);
    }

    /// <summary>
    /// Column information for validation and editing rules.
    /// </summary>
    public IList<ColumnInfo>? ColumnInfos
    {
        get => GetValue(ColumnInfosProperty);
        set => SetValue(ColumnInfosProperty, value);
    }

    /// <summary>
    /// The currently selected row.
    /// </summary>
    public DataRowView? SelectedRowView
    {
        get => GetValue(SelectedRowViewProperty);
        set => SetValue(SelectedRowViewProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    #endregion
}

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

    #region Properties

    public DataRowView RowView { get; }
    
    public string ColumnName { get; }
    
    public object? NewValue { get; }

    #endregion
}

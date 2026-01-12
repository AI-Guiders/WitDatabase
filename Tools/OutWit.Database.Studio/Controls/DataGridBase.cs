using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using OutWit.Common.MVVM.Attributes;
using OutWit.Database.Studio.Converters;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// Base class for DataGrids that display DataView results.
/// Provides common functionality for column generation and NULL value styling.
/// </summary>
public abstract partial class DataGridBase : DataGrid
{
    #region Static

    static DataGridBase()
    {
        ResultViewProperty.Changed.AddClassHandler<DataGridBase>((grid, e) => grid.OnResultViewChanged(e));
    }

    #endregion

    #region Converters

    protected static SqlValueConverter m_valueConverter = new();

    private static SqlValueBrushConverter m_brushConverter = new();

    #endregion

    #region Fields

    private readonly List<IStyle> m_dynamicStyles = [];

    #endregion

    #region Constructors

    protected DataGridBase()
    {
        InitDefaults();
    }

    #endregion

    #region Initialization

    private void InitDefaults()
    {
        AutoGenerateColumns = false;
        GridLinesVisibility = DataGridGridLinesVisibility.All;
        CanUserResizeColumns = true;
        CanUserReorderColumns = true;
        CanUserSortColumns = true;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Clears dynamically created styles.
    /// </summary>
    protected void ClearDynamicStyles()
    {
        foreach (var style in m_dynamicStyles)
            Styles.Remove(style);

        m_dynamicStyles.Clear();
    }

    /// <summary>
    /// Creates a column for the specified DataColumn.
    /// </summary>
    protected virtual DataGridTextColumn CreateColumn(DataColumn dataColumn, int ordinal, string className)
    {
        return new DataGridTextColumn
        {
            Header = dataColumn.ColumnName,
            Binding = new Binding($"Row.ItemArray[{ordinal}]")
            {
                Converter = m_valueConverter,
                Mode = BindingMode.OneWay
            },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 50,
            CanUserSort = true,
            Tag = ordinal
        };
    }

    /// <summary>
    /// Creates a style for NULL value display in the specified column.
    /// </summary>
    protected IStyle CreateNullValueStyle(int ordinal, string className)
    {
        var cellStyle = new Style(x => x.OfType<DataGridCell>().Class(className));
        var foregroundBinding = new Binding($"Row.ItemArray[{ordinal}]")
        {
            Converter = m_brushConverter,
        };
        cellStyle.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, foregroundBinding));
        return cellStyle;
    }

    /// <summary>
    /// Rebuilds columns based on the current ResultView.
    /// </summary>
    protected virtual void RebuildColumns()
    {
        Columns.Clear();
        ClearDynamicStyles();

        if (ResultView?.Table == null)
        {
            ItemsSource = null;
            return;
        }

        foreach (DataColumn col in ResultView.Table.Columns)
        {
            var ordinal = col.Ordinal;
            var className = GetColumnClassName(ordinal);

            var dataGridColumn = CreateColumn(col, ordinal, className);
            dataGridColumn.CellStyleClasses.Add(className);
            Columns.Add(dataGridColumn);

            var cellStyle = CreateNullValueStyle(ordinal, className);
            Styles.Add(cellStyle);
            m_dynamicStyles.Add(cellStyle);
        }

        ItemsSource = ResultView;
    }

    /// <summary>
    /// Gets the CSS class name for a column.
    /// </summary>
    protected virtual string GetColumnClassName(int ordinal)
    {
        return $"col-{ordinal}";
    }

    /// <summary>
    /// Called when ResultView property changes.
    /// </summary>
    protected virtual void OnResultViewChanged(AvaloniaPropertyChangedEventArgs e)
    {
        RebuildColumns();
    }

    #endregion

    #region Properties

    /// <summary>
    /// The DataView to display.
    /// </summary>
    [StyledProperty]
    public DataView? ResultView { get; set; }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    #endregion
}

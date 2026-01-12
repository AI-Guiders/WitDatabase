using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using OutWit.Common.MVVM.Attributes;
using OutWit.Database.Studio.Converters;
using OutWit.Database.Studio.Models;

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
        ColumnSettingsProperty.Changed.AddClassHandler<DataGridBase>((grid, e) => grid.OnColumnSettingsChanged(e));
    }

    #endregion

    #region Converters

    protected static SqlValueConverter m_valueConverter = new();

    private static SqlValueBrushConverter m_brushConverter = new();

    #endregion

    #region Fields

    private readonly List<IStyle> m_dynamicStyles = [];
    private bool m_isUpdatingFromSettings;
    private bool m_isUpdatingSettings;

    #endregion

    #region Constructors

    protected DataGridBase()
    {
        InitDefaults();
        InitEvents();
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

    private void InitEvents()
    {
        ColumnReordered += OnColumnReordered;
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
        var column = new DataGridTextColumn
        {
            Header = dataColumn.ColumnName,
            Binding = new Binding($"Row.ItemArray[{ordinal}]")
            {
                Converter = m_valueConverter,
                Mode = BindingMode.OneWay
            },
            MinWidth = 50,
            CanUserSort = true,
            Tag = ordinal
        };

        // Apply saved width if available
        if (ColumnSettings != null)
        {
            var settings = ColumnSettings.GetOrCreate(dataColumn.ColumnName);
            if (settings.Width.HasValue && settings.Width.Value > 0)
            {
                column.Width = new DataGridLength(settings.Width.Value);
            }
            else
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
        else
        {
            column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        }

        return column;
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
        // Save current column widths before rebuilding
        SaveCurrentColumnWidths();

        Columns.Clear();
        ClearDynamicStyles();

        if (ResultView?.Table == null)
        {
            ItemsSource = null;
            return;
        }

        m_isUpdatingFromSettings = true;
        try
        {
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

            // Apply sort if saved
            ApplySavedSort();
        }
        finally
        {
            m_isUpdatingFromSettings = false;
        }
    }

    /// <summary>
    /// Saves current column widths to ColumnSettings.
    /// </summary>
    private void SaveCurrentColumnWidths()
    {
        if (ColumnSettings == null || m_isUpdatingFromSettings)
            return;

        m_isUpdatingSettings = true;
        try
        {
            foreach (var column in Columns)
            {
                if (column.Header is string headerName)
                {
                    var settings = ColumnSettings.GetOrCreate(headerName);
                    settings.Width = column.ActualWidth > 0 ? column.ActualWidth : null;
                    settings.DisplayIndex = column.DisplayIndex;
                }
            }
        }
        finally
        {
            m_isUpdatingSettings = false;
        }
    }

    /// <summary>
    /// Applies saved sort from ColumnSettings.
    /// </summary>
    private void ApplySavedSort()
    {
        if (ColumnSettings == null || string.IsNullOrEmpty(ColumnSettings.SortColumn))
            return;

        if (ResultView == null)
            return;

        var sortDirection = ColumnSettings.SortAscending ? "ASC" : "DESC";
        ResultView.Sort = $"[{ColumnSettings.SortColumn}] {sortDirection}";
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

    /// <summary>
    /// Called when ColumnSettings property changes.
    /// </summary>
    protected virtual void OnColumnSettingsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        // If we have data, rebuild to apply new settings
        if (ResultView?.Table != null)
        {
            RebuildColumns();
        }
    }

    #endregion

    #region Event Handlers

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (ColumnSettings == null || m_isUpdatingFromSettings)
            return;

        SaveCurrentColumnWidths();
    }

    /// <summary>
    /// Called by derived classes when column width changes.
    /// </summary>
    protected void NotifyColumnWidthChanged(DataGridColumn column)
    {
        if (ColumnSettings == null || m_isUpdatingFromSettings || m_isUpdatingSettings)
            return;

        if (column.Header is string headerName)
        {
            var settings = ColumnSettings.GetOrCreate(headerName);
            settings.Width = column.ActualWidth > 0 ? column.ActualWidth : null;
        }
    }

    /// <summary>
    /// Called by derived classes when sort changes.
    /// </summary>
    protected void NotifySortChanged(string? columnName, bool ascending)
    {
        if (ColumnSettings == null || m_isUpdatingFromSettings)
            return;

        ColumnSettings.SortColumn = columnName;
        ColumnSettings.SortAscending = ascending;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The DataView to display.
    /// </summary>
    [StyledProperty]
    public DataView? ResultView { get; set; }

    /// <summary>
    /// Column settings for persistence.
    /// </summary>
    [StyledProperty]
    public GridColumnSettings? ColumnSettings { get; set; }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    #endregion
}

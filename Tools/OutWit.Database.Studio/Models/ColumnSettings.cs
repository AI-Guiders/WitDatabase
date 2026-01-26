namespace OutWit.Database.Studio.Models;

/// <summary>
/// Stores display settings for a DataGrid column.
/// </summary>
public class ColumnSettings
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column width in pixels.
    /// If null, auto-size is used.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Gets or sets the display index (column order).
    /// </summary>
    public int DisplayIndex { get; set; }

    /// <summary>
    /// Gets or sets whether the column is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Stores all column settings for a grid.
/// </summary>
public class GridColumnSettings
{
    /// <summary>
    /// Column settings indexed by column name.
    /// </summary>
    public Dictionary<string, ColumnSettings> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sort column name, or null if not sorted.
    /// </summary>
    public string? SortColumn { get; set; }

    /// <summary>
    /// Sort direction (true = ascending, false = descending).
    /// </summary>
    public bool SortAscending { get; set; } = true;

    /// <summary>
    /// Gets or updates settings for a column.
    /// </summary>
    public ColumnSettings GetOrCreate(string columnName)
    {
        if (!Columns.TryGetValue(columnName, out var settings))
        {
            settings = new ColumnSettings { Name = columnName };
            Columns[columnName] = settings;
        }
        return settings;
    }

    /// <summary>
    /// Clears all settings.
    /// </summary>
    public void Clear()
    {
        Columns.Clear();
        SortColumn = null;
        SortAscending = true;
    }
}

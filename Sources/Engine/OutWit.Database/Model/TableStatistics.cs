using OutWit.Database.Parser.Schema.TableSources;

namespace OutWit.Database.Model;

/// <summary>
/// Statistics about a table for join optimization.
/// </summary>
public sealed class TableStatistics
{
    /// <summary>
    /// The actual table name.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// The alias used in the query (or table name if no alias).
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Estimated number of rows in the table.
    /// </summary>
    public long EstimatedRowCount { get; init; }

    /// <summary>
    /// The original table source.
    /// </summary>
    public required TableSource Source { get; init; }
}
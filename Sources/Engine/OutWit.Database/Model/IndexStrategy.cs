using OutWit.Database.Definitions;
using OutWit.Database.Parser.Expressions;

namespace OutWit.Database.Model;

/// <summary>
/// The recommended index access strategy.
/// </summary>
public sealed class IndexStrategy
{
    /// <summary>
    /// The index to use.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// The table name.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// The index definition.
    /// </summary>
    public required DefinitionIndex IndexDefinition { get; init; }

    /// <summary>
    /// The type of index access.
    /// </summary>
    public IndexAccessType AccessType { get; set; }

    /// <summary>
    /// For seeks, the value to seek (single-column index).
    /// </summary>
    public WitSqlExpression? SeekValue { get; set; }

    /// <summary>
    /// For composite index seeks, the values to seek (one per index column, in column order).
    /// When set, takes precedence over <see cref="SeekValue"/>.
    /// </summary>
    public List<WitSqlExpression>? SeekValues { get; set; }

    /// <summary>
    /// For range scans, the start of the range.
    /// </summary>
    public WitSqlExpression? RangeStart { get; set; }

    /// <summary>
    /// Whether the range start is inclusive.
    /// </summary>
    public bool RangeStartInclusive { get; set; }

    /// <summary>
    /// For range scans, the end of the range.
    /// </summary>
    public WitSqlExpression? RangeEnd { get; set; }

    /// <summary>
    /// Whether the range end is inclusive.
    /// </summary>
    public bool RangeEndInclusive { get; set; }

    /// <summary>
    /// The predicate that matched this index.
    /// </summary>
    public required PredicateInfo MatchedPredicate { get; set; }

    /// <summary>
    /// Estimated cost of using this strategy.
    /// </summary>
    public double EstimatedCost { get; set; }

    /// <summary>
    /// Estimated number of rows returned.
    /// </summary>
    public long EstimatedRowsReturned { get; set; }
}

/// <summary>
/// Types of index access.
/// </summary>
public enum IndexAccessType
{
    /// <summary>
    /// Equality lookup - single key value.
    /// </summary>
    Seek,

    /// <summary>
    /// Range scan - between two key values.
    /// </summary>
    RangeScan
}
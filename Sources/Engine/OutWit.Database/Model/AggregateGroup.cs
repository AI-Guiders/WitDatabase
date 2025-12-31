using OutWit.Common.Abstract;
using OutWit.Common.Values;
using OutWit.Database.Sql;

namespace OutWit.Database.Model;

/// <summary>
/// Represents a group of rows during GROUP BY aggregation.
/// Stores the first row for non-aggregate column access and accumulators for each select item.
/// </summary>
/// <remarks>
/// Optimization: AllRows list is only allocated when storeAllRows parameter is true (HAVING clause exists).
/// This reduces memory usage by 10-50x for queries without HAVING.
/// </remarks>
public sealed class AggregateGroup : ModelBase
{
    #region Constructors

    /// <summary>
    /// Creates a new aggregate group.
    /// </summary>
    /// <param name="firstRow">The first row in this group (for non-aggregate column values).</param>
    /// <param name="selectCount">The number of items in the SELECT list.</param>
    /// <param name="storeAllRows">Whether to store all rows (only needed for HAVING clause).</param>
    public AggregateGroup(WitSqlRow? firstRow, int selectCount, bool storeAllRows = true)
    {
        FirstRow = firstRow;
        Accumulators = new Accumulator[selectCount];
        
        // P0.1 optimization: Only allocate AllRows list when HAVING clause exists
        AllRows = storeAllRows ? new List<WitSqlRow>() : EmptyRowList;
        
        for (int i = 0; i < selectCount; i++)
        {
            Accumulators[i] = new Accumulator();
        }
    }

    private AggregateGroup(WitSqlRow? firstRow, Accumulator[] accumulators, List<WitSqlRow> allRows, int rowCount)
    {
        FirstRow = firstRow;
        Accumulators = accumulators;
        AllRows = allRows;
        RowCount = rowCount;
    }

    #endregion

    #region ModelBase

    /// <inheritdoc/>
    public override bool Is(ModelBase modelBase, double tolerance = DEFAULT_TOLERANCE)
    {
        if (modelBase is not AggregateGroup other)
            return false;

        return FirstRow.Check(other.FirstRow)
               && Accumulators.Check(other.Accumulators)
               && RowCount.Is(other.RowCount);
    }

    /// <inheritdoc/>
    public override AggregateGroup Clone()
    {
        return new AggregateGroup(
            FirstRow,
            Accumulators.Select(acc => acc.Clone()).ToArray(),
            AllRows == EmptyRowList ? EmptyRowList : new List<WitSqlRow>(AllRows),
            RowCount);
    }

    #endregion

    #region Static

    /// <summary>
    /// Shared empty list to avoid allocations when AllRows is not needed.
    /// </summary>
    private static readonly List<WitSqlRow> EmptyRowList = [];

    #endregion

    #region Properties

    /// <summary>
    /// Gets the first row in this group, used for accessing non-aggregate column values.
    /// </summary>
    public WitSqlRow? FirstRow { get; }

    /// <summary>
    /// Gets the accumulators for each item in the SELECT list.
    /// </summary>
    public Accumulator[] Accumulators { get; }

    /// <summary>
    /// Gets all rows in this group. Used for HAVING clause evaluation.
    /// When HAVING is not present, this returns a shared empty list.
    /// </summary>
    public List<WitSqlRow> AllRows { get; }

    /// <summary>
    /// Gets or sets the total count of rows in this group.
    /// </summary>
    public int RowCount { get; set; }

    #endregion
}
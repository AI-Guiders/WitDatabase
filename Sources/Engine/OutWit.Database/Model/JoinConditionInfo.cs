namespace OutWit.Database.Model;

/// <summary>
/// Information about a join condition for optimization.
/// </summary>
public sealed class JoinConditionInfo
{
    /// <summary>
    /// The left table alias in the join condition.
    /// </summary>
    public required string LeftTableAlias { get; init; }

    /// <summary>
    /// The left column name in the join condition.
    /// </summary>
    public required string LeftColumnName { get; init; }

    /// <summary>
    /// The right table alias in the join condition.
    /// </summary>
    public required string RightTableAlias { get; init; }

    /// <summary>
    /// The right column name in the join condition.
    /// </summary>
    public required string RightColumnName { get; init; }

    /// <summary>
    /// Whether this is an equality join on a primary key or unique column.
    /// </summary>
    public bool IsPrimaryKeyJoin { get; init; }
}
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;

namespace OutWit.Database.Model;

/// <summary>
/// Information about an extracted predicate.
/// </summary>
public sealed class PredicateInfo
{
    /// <summary>
    /// The column name being compared.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// The table alias (if qualified reference).
    /// </summary>
    public string? TableAlias { get; init; }

    /// <summary>
    /// The comparison operator.
    /// </summary>
    public required BinaryOperatorType Operator { get; init; }

    /// <summary>
    /// The value being compared to (literal or parameter).
    /// </summary>
    public required WitSqlExpression CompareValue { get; init; }

    /// <summary>
    /// The original expression for reference.
    /// </summary>
    public required WitSqlExpression OriginalExpression { get; init; }

    /// <summary>
    /// For expression indexes, the normalized expression text.
    /// </summary>
    public string? ExpressionText { get; init; }
}
using OutWit.Database.Iterators;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;

namespace OutWit.Database.Optimizers;

/// <summary>
/// Analyzes join conditions to extract equi-join keys and residual conditions.
/// Used by query planner to select optimal join algorithm.
/// </summary>
public static class OptimizerJoinCondition
{
    #region Public Methods

    /// <summary>
    /// Analyzes a join ON condition and extracts equi-join keys.
    /// </summary>
    /// <param name="onCondition">The ON condition expression.</param>
    /// <returns>Result containing equi-join keys and any residual conditions.</returns>
    public static JoinConditionAnalysis Analyze(WitSqlExpression? onCondition)
    {
        if (onCondition == null)
        {
            return new JoinConditionAnalysis
            {
                EquiJoinKeys = [],
                ResidualCondition = null
            };
        }

        var equiKeys = new List<IteratorHashJoin.JoinKeyPair>();
        var residualParts = new List<WitSqlExpression>();

        AnalyzeRecursive(onCondition, equiKeys, residualParts);

        return new JoinConditionAnalysis
        {
            EquiJoinKeys = equiKeys,
            ResidualCondition = CombineWithAnd(residualParts)
        };
    }

    /// <summary>
    /// Determines if hash join should be used based on table sizes and join condition.
    /// </summary>
    /// <param name="leftRowCount">Estimated row count of left table.</param>
    /// <param name="rightRowCount">Estimated row count of right table.</param>
    /// <param name="analysis">The join condition analysis.</param>
    /// <returns>True if hash join is preferred over nested loop.</returns>
    public static bool ShouldUseHashJoin(long leftRowCount, long rightRowCount, JoinConditionAnalysis analysis)
    {
        // Hash join requires at least one equi-join key
        if (analysis.EquiJoinKeys.Count == 0)
            return false;

        // Hash join has build overhead, only beneficial for larger tables
        // Threshold: if nested loop would do more than ~1000 comparisons
        const long HASH_JOIN_THRESHOLD = 32;

        var smallerTable = Math.Min(leftRowCount, rightRowCount);
        var largerTable = Math.Max(leftRowCount, rightRowCount);

        // Nested loop cost: O(N × M)
        // Hash join cost: O(N + M) + hash overhead
        // Use hash join when N × M > threshold × (N + M)
        var nestedLoopCost = leftRowCount * rightRowCount;
        var hashJoinCost = (leftRowCount + rightRowCount) * HASH_JOIN_THRESHOLD;

        return nestedLoopCost > hashJoinCost;
    }

    /// <summary>
    /// Determines which side should be the build side for hash join.
    /// Generally the smaller table should be build side.
    /// </summary>
    /// <param name="leftRowCount">Estimated row count of left table.</param>
    /// <param name="rightRowCount">Estimated row count of right table.</param>
    /// <returns>True if left should be build side, false for right.</returns>
    public static bool ShouldBuildLeft(long leftRowCount, long rightRowCount)
    {
        // Build the smaller table into hash table
        // This minimizes memory usage and hash table lookups
        return leftRowCount <= rightRowCount;
    }

    #endregion

    #region Private Methods

    private static void AnalyzeRecursive(
        WitSqlExpression expression,
        List<IteratorHashJoin.JoinKeyPair> equiKeys,
        List<WitSqlExpression> residualParts)
    {
        switch (expression)
        {
            case WitSqlExpressionBinary binary:
                if (binary.Operator == BinaryOperatorType.And)
                {
                    // Recursively process AND conditions
                    AnalyzeRecursive(binary.Left, equiKeys, residualParts);
                    AnalyzeRecursive(binary.Right, equiKeys, residualParts);
                }
                else if (binary.Operator == BinaryOperatorType.Equal)
                {
                    // Check if this is an equi-join condition (column = column)
                    if (TryExtractEquiJoinKey(binary, out var keyPair))
                    {
                        equiKeys.Add(keyPair!);
                    }
                    else
                    {
                        // Not a simple column = column, add to residual
                        residualParts.Add(expression);
                    }
                }
                else
                {
                    // Other operators go to residual
                    residualParts.Add(expression);
                }
                break;

            default:
                // All other expressions are residual
                residualParts.Add(expression);
                break;
        }
    }

    private static bool TryExtractEquiJoinKey(
        WitSqlExpressionBinary binary,
        out IteratorHashJoin.JoinKeyPair? keyPair)
    {
        keyPair = null;

        if (binary.Operator != BinaryOperatorType.Equal)
            return false;

        // Both sides must be column references from different tables
        if (binary.Left is not WitSqlExpressionColumnRef leftCol ||
            binary.Right is not WitSqlExpressionColumnRef rightCol)
            return false;

        // Must have table qualifiers to distinguish join sides
        // If no table qualifier, we can't determine which side the column belongs to
        // In that case, treat as residual and let IteratorJoin handle it
        if (leftCol.TableName == null && rightCol.TableName == null)
            return false;

        // If both have same table name, it's not a join condition
        if (leftCol.TableName != null && rightCol.TableName != null &&
            leftCol.TableName.Equals(rightCol.TableName, StringComparison.OrdinalIgnoreCase))
            return false;

        keyPair = new IteratorHashJoin.JoinKeyPair
        {
            LeftKey = binary.Left,
            RightKey = binary.Right
        };

        return true;
    }

    private static WitSqlExpression? CombineWithAnd(List<WitSqlExpression> parts)
    {
        if (parts.Count == 0)
            return null;

        if (parts.Count == 1)
            return parts[0];

        // Build right-associative AND chain
        var result = parts[^1];
        for (int i = parts.Count - 2; i >= 0; i--)
        {
            result = new WitSqlExpressionBinary
            {
                Left = parts[i],
                Operator = BinaryOperatorType.And,
                Right = result
            };
        }

        return result;
    }

    #endregion
}

/// <summary>
/// Result of analyzing a join condition.
/// </summary>
public sealed class JoinConditionAnalysis
{
    /// <summary>
    /// Equi-join key pairs extracted from the condition.
    /// </summary>
    public required IReadOnlyList<IteratorHashJoin.JoinKeyPair> EquiJoinKeys { get; init; }

    /// <summary>
    /// Remaining conditions that couldn't be converted to equi-join keys.
    /// </summary>
    public WitSqlExpression? ResidualCondition { get; init; }

    /// <summary>
    /// True if any equi-join keys were found.
    /// </summary>
    public bool HasEquiJoinKeys => EquiJoinKeys.Count > 0;
}

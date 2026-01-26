using OutWit.Database.Definitions;
using OutWit.Database.Model;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;

namespace OutWit.Database.Optimizers;

/// <summary>
/// Query optimizer that selects indexes and pushes predicates.
/// Analyzes WHERE clauses to find the best execution strategy.
/// </summary>
public sealed class OptimizerQuery
{
    #region Constants

    /// <summary>
    /// Estimated cost for a full table scan per row.
    /// </summary>
    private const double TABLE_SCAN_COST_PER_ROW = 1.0;

    /// <summary>
    /// Estimated cost for an index seek (equality lookup).
    /// Much cheaper than full scan for selective predicates.
    /// </summary>
    private const double INDEX_SEEK_BASE_COST = 5.0;

    /// <summary>
    /// Additional cost per estimated row returned.
    /// This makes unique indexes cheaper than non-unique ones.
    /// </summary>
    private const double INDEX_FETCH_COST_PER_ROW = 1.0;

    /// <summary>
    /// Estimated cost for an index range scan per row.
    /// Cheaper than table scan but more than equality lookup.
    /// </summary>
    private const double INDEX_RANGE_COST_PER_ROW = 0.5;

    /// <summary>
    /// Default selectivity estimate when we don't have statistics.
    /// Assumes 10% of rows match a typical predicate.
    /// </summary>
    private const double DEFAULT_SELECTIVITY = 0.1;

    /// <summary>
    /// Selectivity estimate for equality predicates.
    /// Assumes 1% of rows match (higher selectivity = more selective).
    /// </summary>
    private const double EQUALITY_SELECTIVITY = 0.01;

    /// <summary>
    /// Selectivity estimate for range predicates.
    /// Assumes 20% of rows match.
    /// </summary>
    private const double RANGE_SELECTIVITY = 0.2;

    #endregion

    #region Functions

    /// <summary>
    /// Analyzes a WHERE clause and finds the best index to use.
    /// </summary>
    /// <param name="tableName">The table being queried.</param>
    /// <param name="whereClause">The WHERE clause expression.</param>
    /// <param name="availableIndexes">Available indexes on the table.</param>
    /// <param name="estimatedRowCount">Estimated row count in the table.</param>
    /// <returns>The best index strategy, or null if table scan is preferred.</returns>
    public IndexStrategy? FindBestIndex(
        string tableName,
        WitSqlExpression? whereClause,
        IEnumerable<DefinitionIndex> availableIndexes,
        long estimatedRowCount)
    {
        if (whereClause == null || estimatedRowCount <= 0)
            return null;

        // Extract predicates from WHERE clause
        var predicates = ExtractPredicates(whereClause);
        if (predicates.Count == 0)
            return null;

        // Calculate cost for each index
        IndexStrategy? bestStrategy = null;
        double bestCost = estimatedRowCount * TABLE_SCAN_COST_PER_ROW; // Base case: full table scan

        foreach (var index in availableIndexes)
        {
            // Skip primary key index - we use row ID directly
            if (index.IsPrimaryKey)
                continue;

            var strategy = EvaluateIndex(index, predicates, estimatedRowCount);
            if (strategy != null && strategy.EstimatedCost < bestCost)
            {
                bestStrategy = strategy;
                bestCost = strategy.EstimatedCost;
            }
        }

        return bestStrategy;
    }

    /// <summary>
    /// Evaluates whether an index is useful for the given predicates.
    /// </summary>
    private IndexStrategy? EvaluateIndex(
        DefinitionIndex index,
        IReadOnlyList<PredicateInfo> predicates,
        long estimatedRowCount)
    {
        // Find predicates that match the index's first column
        // (For composite indexes, we need to match from the leftmost column)
        var firstColumn = index.Columns[0];
        var matchingPredicate = FindMatchingPredicate(firstColumn, index.GetColumnExpression(0), predicates);

        if (matchingPredicate == null)
            return null;

        // Check if index is filtered (partial) and predicate matches filter
        if (index.IsFiltered)
        {
            // For filtered indexes, we'd need to check if the predicate matches
            // For now, skip filtered indexes in automatic selection
            // They can still be used with explicit hints
            return null;
        }

        var strategy = new IndexStrategy
        {
            IndexName = index.Name,
            TableName = index.TableName,
            IndexDefinition = index,
            MatchedPredicate = matchingPredicate
        };

        // Determine access type based on predicate operator
        switch (matchingPredicate.Operator)
        {
            case BinaryOperatorType.Equal:
                strategy.AccessType = IndexAccessType.Seek;
                strategy.SeekValue = matchingPredicate.CompareValue;
                
                // For unique index, at most 1 row
                if (index.IsUnique)
                {
                    strategy.EstimatedRowsReturned = 1;
                }
                else
                {
                    strategy.EstimatedRowsReturned = Math.Max(1, (long)(estimatedRowCount * EQUALITY_SELECTIVITY));
                }
                
                // Cost = base seek cost + row fetch cost
                strategy.EstimatedCost = INDEX_SEEK_BASE_COST + (strategy.EstimatedRowsReturned * INDEX_FETCH_COST_PER_ROW);
                break;

            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.LessOrEqual:
                strategy.AccessType = IndexAccessType.RangeScan;
                strategy.RangeEnd = matchingPredicate.CompareValue;
                strategy.RangeEndInclusive = matchingPredicate.Operator == BinaryOperatorType.LessOrEqual;
                strategy.EstimatedRowsReturned = Math.Max(1, (long)(estimatedRowCount * RANGE_SELECTIVITY));
                strategy.EstimatedCost = strategy.EstimatedRowsReturned * INDEX_RANGE_COST_PER_ROW;
                break;

            case BinaryOperatorType.GreaterThan:
            case BinaryOperatorType.GreaterOrEqual:
                strategy.AccessType = IndexAccessType.RangeScan;
                strategy.RangeStart = matchingPredicate.CompareValue;
                strategy.RangeStartInclusive = matchingPredicate.Operator == BinaryOperatorType.GreaterOrEqual;
                strategy.EstimatedRowsReturned = Math.Max(1, (long)(estimatedRowCount * RANGE_SELECTIVITY));
                strategy.EstimatedCost = strategy.EstimatedRowsReturned * INDEX_RANGE_COST_PER_ROW;
                break;

            default:
                // Operator not suitable for index scan
                return null;
        }

        // Check for BETWEEN (combined predicates on same column)
        TryOptimizeForBetween(strategy, firstColumn, predicates, estimatedRowCount);

        return strategy;
    }

    /// <summary>
    /// Tries to optimize the strategy if there are both lower and upper bounds (BETWEEN).
    /// </summary>
    private void TryOptimizeForBetween(
        IndexStrategy strategy,
        string columnName,
        IReadOnlyList<PredicateInfo> predicates,
        long estimatedRowCount)
    {
        // If we already have a seek, skip
        if (strategy.AccessType == IndexAccessType.Seek)
            return;

        // Look for complementary predicate
        foreach (var pred in predicates)
        {
            if (!pred.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (pred == strategy.MatchedPredicate)
                continue;

            bool isLowerBound = pred.Operator == BinaryOperatorType.GreaterThan || 
                               pred.Operator == BinaryOperatorType.GreaterOrEqual;
            bool isUpperBound = pred.Operator == BinaryOperatorType.LessThan || 
                               pred.Operator == BinaryOperatorType.LessOrEqual;

            // If we have range end and found lower bound
            if (strategy.RangeEnd != null && isLowerBound)
            {
                strategy.RangeStart = pred.CompareValue;
                strategy.RangeStartInclusive = pred.Operator == BinaryOperatorType.GreaterOrEqual;
                // BETWEEN is more selective
                strategy.EstimatedRowsReturned = (long)(estimatedRowCount * RANGE_SELECTIVITY * 0.5);
                strategy.EstimatedCost = strategy.EstimatedRowsReturned * INDEX_RANGE_COST_PER_ROW;
                break;
            }

            // If we have range start and found upper bound
            if (strategy.RangeStart != null && isUpperBound)
            {
                strategy.RangeEnd = pred.CompareValue;
                strategy.RangeEndInclusive = pred.Operator == BinaryOperatorType.LessOrEqual;
                // BETWEEN is more selective
                strategy.EstimatedRowsReturned = (long)(estimatedRowCount * RANGE_SELECTIVITY * 0.5);
                strategy.EstimatedCost = strategy.EstimatedRowsReturned * INDEX_RANGE_COST_PER_ROW;
                break;
            }
        }
    }

    /// <summary>
    /// Finds a predicate that matches an index column.
    /// </summary>
    private PredicateInfo? FindMatchingPredicate(
        string columnName,
        string? expressionText,
        IReadOnlyList<PredicateInfo> predicates)
    {
        foreach (var pred in predicates)
        {
            // Match column name (simple index)
            if (pred.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure the column is on the left side of the comparison
                // (we already normalize this in ExtractPredicates)
                return pred;
            }

            // For expression indexes (e.g., LOWER(email)), match the expression
            if (expressionText != null && pred.ExpressionText != null)
            {
                if (pred.ExpressionText.Equals(expressionText, StringComparison.OrdinalIgnoreCase))
                    return pred;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts usable predicates from a WHERE clause expression.
    /// </summary>
    private List<PredicateInfo> ExtractPredicates(WitSqlExpression expression)
    {
        var predicates = new List<PredicateInfo>();
        ExtractPredicatesRecursive(expression, predicates);
        return predicates;
    }

    private void ExtractPredicatesRecursive(WitSqlExpression expression, List<PredicateInfo> predicates)
    {
        switch (expression)
        {
            case WitSqlExpressionBinary binary:
                // AND - extract predicates from both sides
                if (binary.Operator == BinaryOperatorType.And)
                {
                    ExtractPredicatesRecursive(binary.Left, predicates);
                    ExtractPredicatesRecursive(binary.Right, predicates);
                    return;
                }

                // Comparison operators
                if (IsComparisonOperator(binary.Operator))
                {
                    var predicate = TryExtractPredicate(binary);
                    if (predicate != null)
                    {
                        predicates.Add(predicate);
                    }
                }
                break;

            case WitSqlExpressionBetween between:
                // BETWEEN is equivalent to two range predicates
                if (between.Expression is WitSqlExpressionColumnRef col && !between.IsNot)
                {
                    if (IsConstant(between.Low))
                    {
                        predicates.Add(new PredicateInfo
                        {
                            ColumnName = col.ColumnName,
                            TableAlias = col.TableName,
                            Operator = BinaryOperatorType.GreaterOrEqual,
                            CompareValue = between.Low,
                            OriginalExpression = between
                        });
                    }
                    if (IsConstant(between.High))
                    {
                        predicates.Add(new PredicateInfo
                        {
                            ColumnName = col.ColumnName,
                            TableAlias = col.TableName,
                            Operator = BinaryOperatorType.LessOrEqual,
                            CompareValue = between.High,
                            OriginalExpression = between
                        });
                    }
                }
                break;

            case WitSqlExpressionIn inExpr:
                // IN with small value list can use index
                if (inExpr.Expression is WitSqlExpressionColumnRef inCol && 
                    !inExpr.IsNot && 
                    inExpr.Values is { Count: 1 } && 
                    IsConstant(inExpr.Values[0]))
                {
                    // Single value IN is equivalent to equality
                    predicates.Add(new PredicateInfo
                    {
                        ColumnName = inCol.ColumnName,
                        TableAlias = inCol.TableName,
                        Operator = BinaryOperatorType.Equal,
                        CompareValue = inExpr.Values[0],
                        OriginalExpression = inExpr
                    });
                }
                break;
        }
    }

    private PredicateInfo? TryExtractPredicate(WitSqlExpressionBinary binary)
    {
        // Check if one side is a column reference and the other is a constant
        WitSqlExpressionColumnRef? column = null;
        WitSqlExpression? value = null;
        var op = binary.Operator;

        if (binary.Left is WitSqlExpressionColumnRef leftCol && IsConstant(binary.Right))
        {
            column = leftCol;
            value = binary.Right;
        }
        else if (binary.Right is WitSqlExpressionColumnRef rightCol && IsConstant(binary.Left))
        {
            column = rightCol;
            value = binary.Left;
            // Flip operator when column is on right
            op = FlipOperator(op);
        }

        // Check for function calls (expression indexes)
        string? expressionText = null;
        if (binary.Left is WitSqlExpressionFunctionCall funcCall && IsConstant(binary.Right))
        {
            // For function indexes like LOWER(email), extract the expression
            expressionText = ExtractFunctionExpression(funcCall);
            if (expressionText != null && funcCall.Arguments is { Count: 1 } && 
                funcCall.Arguments[0] is WitSqlExpressionColumnRef funcColRef)
            {
                column = funcColRef;
                value = binary.Right;
            }
        }

        if (column == null || value == null)
            return null;

        return new PredicateInfo
        {
            ColumnName = column.ColumnName,
            TableAlias = column.TableName,
            Operator = op,
            CompareValue = value,
            OriginalExpression = binary,
            ExpressionText = expressionText
        };
    }

    private static string? ExtractFunctionExpression(WitSqlExpressionFunctionCall func)
    {
        if (func.Arguments is not { Count: 1 } || 
            func.Arguments[0] is not WitSqlExpressionColumnRef colRef)
            return null;

        // Return normalized expression like "LOWER(columnName)"
        return $"{func.FunctionName.ToUpperInvariant()}({colRef.ColumnName})";
    }

    private static bool IsComparisonOperator(BinaryOperatorType op)
    {
        return op switch
        {
            BinaryOperatorType.Equal => true,
            BinaryOperatorType.NotEqual => false, // Not useful for index
            BinaryOperatorType.LessThan => true,
            BinaryOperatorType.LessOrEqual => true,
            BinaryOperatorType.GreaterThan => true,
            BinaryOperatorType.GreaterOrEqual => true,
            _ => false
        };
    }

    private static BinaryOperatorType FlipOperator(BinaryOperatorType op)
    {
        return op switch
        {
            BinaryOperatorType.LessThan => BinaryOperatorType.GreaterThan,
            BinaryOperatorType.LessOrEqual => BinaryOperatorType.GreaterOrEqual,
            BinaryOperatorType.GreaterThan => BinaryOperatorType.LessThan,
            BinaryOperatorType.GreaterOrEqual => BinaryOperatorType.LessOrEqual,
            _ => op // Equal and NotEqual are symmetric
        };
    }

    private static bool IsConstant(WitSqlExpression expression)
    {
        return expression switch
        {
            WitSqlExpressionLiteral => true,
            WitSqlExpressionParameter => true, // Parameters are constant at query time
            _ => false
        };
    }

    #endregion
}
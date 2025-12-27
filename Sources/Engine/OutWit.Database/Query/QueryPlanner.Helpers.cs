using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.TableSources;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Query;

/// <summary>
/// Helper methods for QueryPlanner (aggregate detection, window functions, etc.).
/// </summary>
public sealed partial class QueryPlanner
{
    #region Aggregate Detection

    private bool IsAggregateQuery(WitSqlStatementSelect select)
    {
        // Query is aggregate if it has GROUP BY or any aggregate function in SELECT (without OVER)
        if (select.GroupByClause != null && select.GroupByClause.Count > 0)
            return true;

        return HasNonWindowAggregates(select.SelectList);
    }

    private static bool HasNonWindowAggregates(IReadOnlyList<ClauseSelectItem> selectList)
    {
        foreach (var item in selectList)
        {
            if (ContainsNonWindowAggregateFunction(item.Expression))
                return true;
        }
        return false;
    }

    private static bool ContainsNonWindowAggregateFunction(WitSqlExpression? expression)
    {
        if (expression == null)
            return false;

        return expression switch
        {
            // Aggregate function WITHOUT OVER clause = regular aggregate
            WitSqlExpressionFunctionCall func => 
                AGGREGATE_FUNCTIONS.Contains(func.FunctionName) && func.Over == null,
            WitSqlExpressionBinary binary => 
                ContainsNonWindowAggregateFunction(binary.Left) || ContainsNonWindowAggregateFunction(binary.Right),
            WitSqlExpressionUnary unary => 
                ContainsNonWindowAggregateFunction(unary.Operand),
            WitSqlExpressionCase caseExpr => 
                ContainsNonWindowAggregateFunctionInCase(caseExpr),
            _ => false
        };
    }

    private static bool ContainsNonWindowAggregateFunctionInCase(WitSqlExpressionCase caseExpr)
    {
        if (ContainsNonWindowAggregateFunction(caseExpr.Operand))
            return true;

        foreach (var whenClause in caseExpr.WhenClauses)
        {
            if (ContainsNonWindowAggregateFunction(whenClause.When) || 
                ContainsNonWindowAggregateFunction(whenClause.Then))
                return true;
        }

        return ContainsNonWindowAggregateFunction(caseExpr.ElseResult);
    }

    private static bool HasAggregates(IReadOnlyList<ClauseSelectItem> selectList)
    {
        foreach (var item in selectList)
        {
            if (ContainsAggregateFunction(item.Expression))
                return true;
        }
        return false;
    }

    private static bool ContainsAggregateFunction(WitSqlExpression? expression)
    {
        if (expression == null)
            return false;

        return expression switch
        {
            WitSqlExpressionFunctionCall func => AGGREGATE_FUNCTIONS.Contains(func.FunctionName),
            WitSqlExpressionBinary binary => ContainsAggregateFunction(binary.Left) || ContainsAggregateFunction(binary.Right),
            WitSqlExpressionUnary unary => ContainsAggregateFunction(unary.Operand),
            WitSqlExpressionCase caseExpr => ContainsAggregateFunctionInCase(caseExpr),
            _ => false
        };
    }

    private static bool ContainsAggregateFunctionInCase(WitSqlExpressionCase caseExpr)
    {
        if (ContainsAggregateFunction(caseExpr.Operand))
            return true;

        foreach (var whenClause in caseExpr.WhenClauses)
        {
            if (ContainsAggregateFunction(whenClause.When) || ContainsAggregateFunction(whenClause.Then))
                return true;
        }

        return ContainsAggregateFunction(caseExpr.ElseResult);
    }

    #endregion

    #region Window Function Detection

    /// <summary>
    /// Checks if the SELECT list contains any window functions.
    /// </summary>
    private static bool HasWindowFunctions(IReadOnlyList<ClauseSelectItem> selectList)
    {
        foreach (var item in selectList)
        {
            if (ContainsWindowFunction(item.Expression))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Recursively checks if an expression contains a window function.
    /// </summary>
    private static bool ContainsWindowFunction(WitSqlExpression? expression)
    {
        if (expression == null)
            return false;

        return expression switch
        {
            WitSqlExpressionFunctionCall func => IsWindowFunction(func),
            WitSqlExpressionBinary binary => 
                ContainsWindowFunction(binary.Left) || ContainsWindowFunction(binary.Right),
            WitSqlExpressionUnary unary => 
                ContainsWindowFunction(unary.Operand),
            WitSqlExpressionCase caseExpr => 
                ContainsWindowFunctionInCase(caseExpr),
            _ => false
        };
    }

    private static bool ContainsWindowFunctionInCase(WitSqlExpressionCase caseExpr)
    {
        if (ContainsWindowFunction(caseExpr.Operand))
            return true;

        foreach (var whenClause in caseExpr.WhenClauses)
        {
            if (ContainsWindowFunction(whenClause.When) || 
                ContainsWindowFunction(whenClause.Then))
                return true;
        }

        return ContainsWindowFunction(caseExpr.ElseResult);
    }

    /// <summary>
    /// Determines if a function call is a window function.
    /// A window function is either:
    /// 1. A ranking function (ROW_NUMBER, RANK, etc.) with OVER clause
    /// 2. A value function (LAG, LEAD, etc.) with OVER clause
    /// 3. An aggregate function with OVER clause
    /// </summary>
    private static bool IsWindowFunction(WitSqlExpressionFunctionCall func)
    {
        // Must have OVER clause to be a window function
        if (func.Over == null)
            return false;

        var funcName = func.FunctionName.ToUpperInvariant();

        // Check if it's a ranking or value window function
        if (WINDOW_RANKING_FUNCTIONS.Contains(funcName) || 
            WINDOW_VALUE_FUNCTIONS.Contains(funcName))
            return true;

        // Aggregate functions with OVER are also window functions
        if (AGGREGATE_FUNCTIONS.Contains(funcName))
            return true;

        return false;
    }

    #endregion

    #region SELECT List Helpers

    private static bool IsSelectStar(IReadOnlyList<ClauseSelectItem> selectList)
    {
        return selectList.Count == 1 && selectList[0].IsStar;
    }

    #endregion

    #region Table Name Extraction

    /// <summary>
    /// Gets the primary table name from the FROM clause for locking purposes.
    /// For simple queries, returns the first table. For joins, returns the leftmost table.
    /// </summary>
    private static string? GetPrimaryTableName(WitSqlStatementSelect select)
    {
        if (select.FromClause == null || select.FromClause.Count == 0)
            return null;

        return GetTableNameFromSource(select.FromClause[0]);
    }

    private static string? GetTableNameFromSource(TableSource source)
    {
        return source switch
        {
            TableSourceSimple simple => simple.TableName,
            TableSourceJoin join => GetTableNameFromSource(join.Left),
            TableSourceSubquery => null, // Subqueries don't have a table to lock
            _ => null
        };
    }

    #endregion
}

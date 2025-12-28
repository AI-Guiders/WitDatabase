namespace OutWit.Database.Constants;

/// <summary>
/// Constants for SQL function names and categories.
/// Centralized location for all function-related constants to avoid duplication.
/// </summary>
internal static class SqlFunctions
{
    /// <summary>
    /// Aggregate functions that can be used in SELECT, HAVING, and as window functions.
    /// </summary>
    public static readonly HashSet<string> Aggregates = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", 
        "SUM", 
        "AVG", 
        "MIN", 
        "MAX", 
        "GROUP_CONCAT"
    };

    /// <summary>
    /// Window functions that assign ranking/position to rows.
    /// </summary>
    public static readonly HashSet<string> WindowRanking = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROW_NUMBER", 
        "RANK", 
        "DENSE_RANK", 
        "NTILE", 
        "PERCENT_RANK", 
        "CUME_DIST"
    };

    /// <summary>
    /// Window functions that access values from other rows.
    /// </summary>
    public static readonly HashSet<string> WindowValue = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIRST_VALUE", 
        "LAST_VALUE", 
        "NTH_VALUE", 
        "LAG", 
        "LEAD"
    };

    /// <summary>
    /// Checks if a function name is an aggregate function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is an aggregate function.</returns>
    public static bool IsAggregate(string functionName)
    {
        return Aggregates.Contains(functionName);
    }

    /// <summary>
    /// Checks if a function name is a window ranking function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window ranking function.</returns>
    public static bool IsWindowRanking(string functionName)
    {
        return WindowRanking.Contains(functionName);
    }

    /// <summary>
    /// Checks if a function name is a window value function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window value function.</returns>
    public static bool IsWindowValue(string functionName)
    {
        return WindowValue.Contains(functionName);
    }

    /// <summary>
    /// Checks if a function name is any kind of window function (ranking or value).
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window function.</returns>
    public static bool IsWindowFunction(string functionName)
    {
        return WindowRanking.Contains(functionName) || WindowValue.Contains(functionName);
    }
}

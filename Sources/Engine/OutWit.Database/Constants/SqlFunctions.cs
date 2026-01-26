namespace OutWit.Database.Constants;

/// <summary>
/// Helper class for SQL function validation.
/// Provides backward compatibility and convenience methods.
/// </summary>
internal static class SqlFunctions
{
    #region Aggregate Functions

    /// <summary>
    /// Checks if a function name is an aggregate function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is an aggregate function.</returns>
    public static bool IsAggregate(string functionName)
    {
        return SqlAggregateFunction.IsAnyAggregate(functionName);
    }

    /// <summary>
    /// Tries to parse an aggregate function name.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="function">The parsed function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryGetAggregate(string functionName, out SqlAggregateFunction? function)
    {
        return SqlAggregateFunction.TryParseFunction(functionName, out function);
    }

    #endregion

    #region Window Ranking Functions

    /// <summary>
    /// Checks if a function name is a window ranking function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window ranking function.</returns>
    public static bool IsWindowRanking(string functionName)
    {
        return SqlWindowRankingFunction.IsAnyRanking(functionName);
    }

    /// <summary>
    /// Tries to parse a window ranking function name.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="function">The parsed function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryGetWindowRanking(string functionName, out SqlWindowRankingFunction? function)
    {
        return SqlWindowRankingFunction.TryParseFunction(functionName, out function);
    }

    #endregion

    #region Window Value Functions

    /// <summary>
    /// Checks if a function name is a window value function.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window value function.</returns>
    public static bool IsWindowValue(string functionName)
    {
        return SqlWindowValueFunction.IsAnyValue(functionName);
    }

    /// <summary>
    /// Tries to parse a window value function name.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="function">The parsed function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryGetWindowValue(string functionName, out SqlWindowValueFunction? function)
    {
        return SqlWindowValueFunction.TryParseFunction(functionName, out function);
    }

    #endregion

    #region Window Functions (Combined)

    /// <summary>
    /// Checks if a function name is any kind of window function (ranking or value).
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the function is a window function.</returns>
    public static bool IsWindowFunction(string functionName)
    {
        return IsWindowRanking(functionName) || IsWindowValue(functionName);
    }

    #endregion
}

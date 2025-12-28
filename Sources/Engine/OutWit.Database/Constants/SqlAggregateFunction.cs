using OutWit.Common.Enums;

namespace OutWit.Database.Constants;

/// <summary>
/// SQL aggregate functions that can be used in SELECT, GROUP BY, HAVING, and as window functions.
/// </summary>
public sealed class SqlAggregateFunction : StringEnum<SqlAggregateFunction>
{
    #region Static Constants

    /// <summary>
    /// COUNT(*) or COUNT(expr) - counts rows or non-null values.
    /// </summary>
    public static readonly SqlAggregateFunction Count = new("COUNT");

    /// <summary>
    /// SUM(expr) - calculates sum of numeric values.
    /// </summary>
    public static readonly SqlAggregateFunction Sum = new("SUM");

    /// <summary>
    /// AVG(expr) - calculates average of numeric values.
    /// </summary>
    public static readonly SqlAggregateFunction Avg = new("AVG", "AVERAGE");

    /// <summary>
    /// MIN(expr) - finds minimum value.
    /// </summary>
    public static readonly SqlAggregateFunction Min = new("MIN", "MINIMUM");

    /// <summary>
    /// MAX(expr) - finds maximum value.
    /// </summary>
    public static readonly SqlAggregateFunction Max = new("MAX", "MAXIMUM");

    /// <summary>
    /// GROUP_CONCAT(expr) - concatenates string values with separator.
    /// </summary>
    public static readonly SqlAggregateFunction GroupConcat = new("GROUP_CONCAT", "STRING_AGG");

    #endregion

    #region Constructors

    private SqlAggregateFunction(string value, params string[] variations)
        : base(value)
    {
        Variations = variations;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Checks if the given function name matches this aggregate function (case-insensitive).
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the name matches this function or any of its variations.</returns>
    public bool Is(string? functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return false;

        if (StringComparer.OrdinalIgnoreCase.Equals(Value, functionName))
            return true;

        foreach (var variation in Variations)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(variation, functionName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if any aggregate function matches the given name.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the name matches any aggregate function.</returns>
    public static bool IsAnyAggregate(string? functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return false;

        foreach (var func in GetAll())
        {
            if (func.Is(functionName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to parse a function name to a specific aggregate function.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="result">The parsed aggregate function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParseFunction(string? functionName, out SqlAggregateFunction? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(functionName))
            return false;

        foreach (var func in GetAll())
        {
            if (func.Is(functionName))
            {
                result = func;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Alternative names/variations for this function.
    /// </summary>
    public IReadOnlyCollection<string> Variations { get; }

    #endregion
}

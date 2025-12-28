using OutWit.Common.Enums;

namespace OutWit.Database.Constants;

/// <summary>
/// SQL window value functions that access values from other rows.
/// </summary>
public sealed class SqlWindowValueFunction : StringEnum<SqlWindowValueFunction>
{
    #region Static Constants

    /// <summary>
    /// FIRST_VALUE(expr) - returns first value in window frame.
    /// </summary>
    public static readonly SqlWindowValueFunction FirstValue = new("FIRST_VALUE", "FIRST");

    /// <summary>
    /// LAST_VALUE(expr) - returns last value in window frame.
    /// </summary>
    public static readonly SqlWindowValueFunction LastValue = new("LAST_VALUE", "LAST");

    /// <summary>
    /// NTH_VALUE(expr, n) - returns nth value in window frame.
    /// </summary>
    public static readonly SqlWindowValueFunction NthValue = new("NTH_VALUE");

    /// <summary>
    /// LAG(expr, offset, default) - accesses value from previous row.
    /// </summary>
    public static readonly SqlWindowValueFunction Lag = new("LAG");

    /// <summary>
    /// LEAD(expr, offset, default) - accesses value from next row.
    /// </summary>
    public static readonly SqlWindowValueFunction Lead = new("LEAD");

    #endregion

    #region Constructors

    private SqlWindowValueFunction(string value, params string[] variations)
        : base(value)
    {
        Variations = variations;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Checks if the given function name matches this value function (case-insensitive).
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
    /// Checks if any window value function matches the given name.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the name matches any window value function.</returns>
    public static bool IsAnyValue(string? functionName)
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
    /// Tries to parse a function name to a specific value function.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="result">The parsed value function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParseFunction(string? functionName, out SqlWindowValueFunction? result)
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

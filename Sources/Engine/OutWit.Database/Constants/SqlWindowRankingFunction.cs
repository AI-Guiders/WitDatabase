using OutWit.Common.Enums;

namespace OutWit.Database.Constants;

/// <summary>
/// SQL window ranking functions that assign ranking/position to rows.
/// </summary>
public sealed class SqlWindowRankingFunction : StringEnum<SqlWindowRankingFunction>
{
    #region Static Constants

    /// <summary>
    /// ROW_NUMBER() - assigns unique sequential number to each row.
    /// </summary>
    public static readonly SqlWindowRankingFunction RowNumber = new("ROW_NUMBER");

    /// <summary>
    /// RANK() - assigns rank with gaps (1, 2, 2, 4).
    /// </summary>
    public static readonly SqlWindowRankingFunction Rank = new("RANK");

    /// <summary>
    /// DENSE_RANK() - assigns rank without gaps (1, 2, 2, 3).
    /// </summary>
    public static readonly SqlWindowRankingFunction DenseRank = new("DENSE_RANK");

    /// <summary>
    /// NTILE(n) - distributes rows into n buckets.
    /// </summary>
    public static readonly SqlWindowRankingFunction Ntile = new("NTILE");

    /// <summary>
    /// PERCENT_RANK() - calculates relative rank (0 to 1).
    /// </summary>
    public static readonly SqlWindowRankingFunction PercentRank = new("PERCENT_RANK");

    /// <summary>
    /// CUME_DIST() - calculates cumulative distribution.
    /// </summary>
    public static readonly SqlWindowRankingFunction CumeDist = new("CUME_DIST", "CUMULATIVE_DISTRIBUTION");

    #endregion

    #region Constructors

    private SqlWindowRankingFunction(string value, params string[] variations)
        : base(value)
    {
        Variations = variations;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Checks if the given function name matches this ranking function (case-insensitive).
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
    /// Checks if any window ranking function matches the given name.
    /// </summary>
    /// <param name="functionName">The function name to check.</param>
    /// <returns>True if the name matches any window ranking function.</returns>
    public static bool IsAnyRanking(string? functionName)
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
    /// Tries to parse a function name to a specific ranking function.
    /// </summary>
    /// <param name="functionName">The function name to parse.</param>
    /// <param name="result">The parsed ranking function, or null if not found.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParseFunction(string? functionName, out SqlWindowRankingFunction? result)
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

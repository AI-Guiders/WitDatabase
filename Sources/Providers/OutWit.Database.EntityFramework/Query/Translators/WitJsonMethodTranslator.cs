using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace OutWit.Database.EntityFramework.Query.Translators;

/// <summary>
/// Translates JSON-related method calls to WitSQL JSON functions.
/// </summary>
public sealed class WitJsonMethodTranslator : IMethodCallTranslator
{
    #region Constants

    private static readonly MethodInfo s_jsonValueMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonValue), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo s_jsonQueryMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonQuery), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo s_jsonContainsMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonContains), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo s_jsonLengthMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonLength), new[] { typeof(string) })!;

    private static readonly MethodInfo s_jsonTypeMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonType), new[] { typeof(string) })!;

    private static readonly MethodInfo s_jsonValidMethod = typeof(WitJsonExtensions)
        .GetMethod(nameof(WitJsonExtensions.JsonValid), new[] { typeof(string) })!;

    #endregion

    #region Fields

    private readonly ISqlExpressionFactory m_sqlExpressionFactory;
    private readonly IRelationalTypeMappingSource m_typeMappingSource;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitJsonMethodTranslator"/> class.
    /// </summary>
    /// <param name="sqlExpressionFactory">The SQL expression factory.</param>
    /// <param name="typeMappingSource">The type mapping source.</param>
    public WitJsonMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        m_sqlExpressionFactory = sqlExpressionFactory;
        m_typeMappingSource = typeMappingSource;
    }

    #endregion

    #region IMethodCallTranslator

    /// <inheritdoc/>
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method == s_jsonValueMethod)
        {
            return TranslateJsonValue(arguments);
        }

        if (method == s_jsonQueryMethod)
        {
            return TranslateJsonQuery(arguments);
        }

        if (method == s_jsonContainsMethod)
        {
            return TranslateJsonContains(arguments);
        }

        if (method == s_jsonLengthMethod)
        {
            return TranslateJsonLength(arguments);
        }

        if (method == s_jsonTypeMethod)
        {
            return TranslateJsonType(arguments);
        }

        if (method == s_jsonValidMethod)
        {
            return TranslateJsonValid(arguments);
        }

        return null;
    }

    #endregion

    #region Translation Methods

    private SqlExpression TranslateJsonValue(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_VALUE(json, path) -> JSON_EXTRACT(json, path)
        return m_sqlExpressionFactory.Function(
            "JSON_EXTRACT",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true, false },
            typeof(string),
            m_typeMappingSource.FindMapping(typeof(string)));
    }

    private SqlExpression TranslateJsonQuery(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_QUERY(json, path) -> JSON_EXTRACT(json, path)
        return m_sqlExpressionFactory.Function(
            "JSON_EXTRACT",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true, false },
            typeof(string),
            m_typeMappingSource.FindMapping(typeof(string)));
    }

    private SqlExpression TranslateJsonContains(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_CONTAINS(json, value) -> INSTR(json, value) > 0
        var instrCall = m_sqlExpressionFactory.Function(
            "INSTR",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true, true },
            typeof(int),
            m_typeMappingSource.FindMapping(typeof(int)));

        return m_sqlExpressionFactory.GreaterThan(
            instrCall,
            m_sqlExpressionFactory.Constant(0));
    }

    private SqlExpression TranslateJsonLength(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_LENGTH(json) -> JSON_ARRAY_LENGTH(json)
        return m_sqlExpressionFactory.Function(
            "JSON_ARRAY_LENGTH",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true },
            typeof(int),
            m_typeMappingSource.FindMapping(typeof(int)));
    }

    private SqlExpression TranslateJsonType(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_TYPE(json) -> JSON_TYPE(json)
        return m_sqlExpressionFactory.Function(
            "JSON_TYPE",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true },
            typeof(string),
            m_typeMappingSource.FindMapping(typeof(string)));
    }

    private SqlExpression TranslateJsonValid(IReadOnlyList<SqlExpression> arguments)
    {
        // JSON_VALID(json) -> JSON_VALID(json)
        return m_sqlExpressionFactory.Function(
            "JSON_VALID",
            arguments,
            nullable: true,
            argumentsPropagateNullability: new[] { true },
            typeof(bool),
            m_typeMappingSource.FindMapping(typeof(bool)));
    }

    #endregion
}

/// <summary>
/// Extension methods for JSON operations in LINQ queries.
/// </summary>
public static class WitJsonExtensions
{
    /// <summary>
    /// Extracts a scalar value from a JSON document at the specified path.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <param name="path">The JSON path expression.</param>
    /// <returns>The extracted value as a string.</returns>
    /// <remarks>This method is translated to JSON_EXTRACT in SQL.</remarks>
    public static string? JsonValue(this string json, string path)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");

    /// <summary>
    /// Extracts a JSON object or array from a JSON document at the specified path.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <param name="path">The JSON path expression.</param>
    /// <returns>The extracted JSON fragment.</returns>
    /// <remarks>This method is translated to JSON_EXTRACT in SQL.</remarks>
    public static string? JsonQuery(this string json, string path)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");

    /// <summary>
    /// Checks if a JSON document contains the specified value.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>True if the value is found, false otherwise.</returns>
    public static bool JsonContains(this string json, string value)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");

    /// <summary>
    /// Returns the length of a JSON array.
    /// </summary>
    /// <param name="json">The JSON array.</param>
    /// <returns>The number of elements in the array.</returns>
    /// <remarks>This method is translated to JSON_ARRAY_LENGTH in SQL.</remarks>
    public static int JsonLength(this string json)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");

    /// <summary>
    /// Returns the type of the outermost JSON value.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <returns>A string indicating the JSON type (object, array, string, integer, real, true, false, null).</returns>
    /// <remarks>This method is translated to JSON_TYPE in SQL.</remarks>
    public static string? JsonType(this string json)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");

    /// <summary>
    /// Checks if a string is valid JSON.
    /// </summary>
    /// <param name="json">The string to validate.</param>
    /// <returns>True if the string is valid JSON, false otherwise.</returns>
    /// <remarks>This method is translated to JSON_VALID in SQL.</remarks>
    public static bool JsonValid(this string json)
        => throw new InvalidOperationException("This method should only be used in LINQ queries.");
}

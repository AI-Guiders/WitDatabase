using System.Globalization;
using System.Text.Json;
using OutWit.Database.Types;

namespace OutWit.Database.Studio.Converters;

/// <summary>
/// Converts string input to appropriate CLR type based on WitSqlType.
/// Uses WitTypeConverter for type mapping consistency.
/// </summary>
public static class SqlValueParser
{
    /// <summary>
    /// Parses a string value to the target CLR type.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="targetType">The target CLR type.</param>
    /// <returns>The parsed value or DBNull.Value for empty/null input.</returns>
    /// <exception cref="FormatException">When parsing fails.</exception>
    public static object? Parse(string? text, Type targetType)
    {
        if (string.IsNullOrEmpty(text))
            return DBNull.Value;

        // Get the underlying type for nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Get the SQL type for this CLR type
        var sqlType = WitTypeConverter.GetSqlType(underlyingType);

        return ParseBySqlType(text, sqlType, underlyingType);
    }

    /// <summary>
    /// Parses a string value based on WitSqlType.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="sqlType">The SQL type.</param>
    /// <returns>The parsed value.</returns>
    public static object? ParseBySqlType(string text, WitSqlType sqlType)
    {
        var clrType = WitTypeConverter.GetClrType(sqlType);
        return ParseBySqlType(text, sqlType, clrType);
    }

    /// <summary>
    /// Parses a string value based on SQL type name (from database schema).
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="sqlTypeName">The SQL type name (e.g., "VARCHAR", "INTEGER", "DATETIME").</param>
    /// <returns>The parsed value.</returns>
    public static object? ParseBySqlTypeName(string text, string sqlTypeName)
    {
        if (string.IsNullOrEmpty(text))
            return DBNull.Value;

        var sqlType = WitTypeConverter.ParseSqlTypeName(sqlTypeName);
        return ParseBySqlType(text, sqlType);
    }

    private static object? ParseBySqlType(string text, WitSqlType sqlType, Type clrType)
    {
        if (string.IsNullOrEmpty(text))
            return DBNull.Value;

        return sqlType switch
        {
            WitSqlType.Null => DBNull.Value,

            WitSqlType.Integer => ParseInteger(text, clrType),

            WitSqlType.Real => ParseReal(text, clrType),

            WitSqlType.Decimal => decimal.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.Boolean => ParseBoolean(text),

            WitSqlType.Text => text,

            WitSqlType.Blob => ParseBlob(text),

            WitSqlType.DateTime => DateTime.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.DateOnly => DateOnly.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.TimeOnly => TimeOnly.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.TimeSpan => TimeSpan.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.Guid => Guid.Parse(text),

            WitSqlType.DateTimeOffset => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture),

            WitSqlType.Json => ParseJson(text),

            WitSqlType.RowVersion => ulong.Parse(text, CultureInfo.InvariantCulture),

            _ => text
        };
    }

    private static object ParseInteger(string text, Type clrType)
    {
        // Parse as long first, then convert to specific integer type
        var longValue = long.Parse(text, CultureInfo.InvariantCulture);

        if (clrType == typeof(sbyte)) return (sbyte)longValue;
        if (clrType == typeof(byte)) return (byte)longValue;
        if (clrType == typeof(short)) return (short)longValue;
        if (clrType == typeof(ushort)) return (ushort)longValue;
        if (clrType == typeof(int)) return (int)longValue;
        if (clrType == typeof(uint)) return (uint)longValue;
        if (clrType == typeof(ulong)) return (ulong)longValue;

        return longValue; // Default to long
    }

    private static object ParseReal(string text, Type clrType)
    {
        // Parse as double first, then convert to specific type
        var doubleValue = double.Parse(text, CultureInfo.InvariantCulture);

        if (clrType == typeof(Half)) return (Half)doubleValue;
        if (clrType == typeof(float)) return (float)doubleValue;

        return doubleValue; // Default to double
    }

    private static bool ParseBoolean(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower switch
        {
            "true" or "yes" or "1" or "on" => true,
            "false" or "no" or "0" or "off" => false,
            _ => bool.Parse(text)
        };
    }

    private static byte[] ParseBlob(string text)
    {
        // Support hex format: X'...' or 0x...
        if (text.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && text.EndsWith("'"))
        {
            text = text[2..^1];
        }
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        // Parse hex string
        if (text.Length % 2 != 0)
            throw new FormatException("Hex string must have even length");

        var bytes = new byte[text.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private static JsonDocument ParseJson(string text)
    {
        return JsonDocument.Parse(text);
    }

    /// <summary>
    /// Tries to parse a string value to the target type.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="targetType">The target CLR type.</param>
    /// <param name="result">The parsed value if successful.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string? text, Type targetType, out object? result)
    {
        try
        {
            result = Parse(text, targetType);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
}

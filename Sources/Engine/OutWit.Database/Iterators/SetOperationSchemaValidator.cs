using OutWit.Database.Types;

namespace OutWit.Database.Iterators;

/// <summary>
/// Helper class for validating schema compatibility in set operations.
/// </summary>
internal static class SetOperationSchemaValidator
{
    /// <summary>
    /// Validates that two schemas are compatible for set operations (UNION, INTERSECT, EXCEPT).
    /// </summary>
    /// <param name="leftSchema">Schema of the left iterator.</param>
    /// <param name="rightSchema">Schema of the right iterator.</param>
    /// <param name="operationName">Name of the operation for error messages (e.g., "UNION", "INTERSECT").</param>
    /// <exception cref="InvalidOperationException">Thrown when schemas are incompatible.</exception>
    public static void ValidateSchemaCompatibility(
        IReadOnlyList<WitSqlColumnInfo> leftSchema,
        IReadOnlyList<WitSqlColumnInfo> rightSchema,
        string operationName)
    {
        // Allow empty schemas (empty iterators)
        if (leftSchema.Count == 0 || rightSchema.Count == 0)
            return;

        if (leftSchema.Count != rightSchema.Count)
        {
            throw new InvalidOperationException(
                $"{operationName} requires both sides to have the same number of columns. " +
                $"Left has {leftSchema.Count} columns, right has {rightSchema.Count} columns.");
        }

        // SQL standard requires that column types be compatible
        for (int i = 0; i < leftSchema.Count; i++)
        {
            var leftType = leftSchema[i].Type;
            var rightType = rightSchema[i].Type;

            if (leftType != rightType && !IsTypeCompatible(leftType, rightType))
            {
                throw new InvalidOperationException(
                    $"{operationName} column type mismatch at position {i + 1}. " +
                    $"Left column '{leftSchema[i].Name}' has type {leftType}, " +
                    $"right column '{rightSchema[i].Name}' has type {rightType}.");
            }
        }
    }

    /// <summary>
    /// Checks if two SQL types are compatible for set operations.
    /// </summary>
    /// <param name="left">First type to check.</param>
    /// <param name="right">Second type to check.</param>
    /// <returns>True if types are compatible, false otherwise.</returns>
    private static bool IsTypeCompatible(WitSqlType left, WitSqlType right)
    {
        // Same types are always compatible
        if (left == right)
            return true;

        // Numeric types are compatible with each other
        if (IsNumericType(left) && IsNumericType(right))
            return true;

        // Date/Time types are compatible with each other
        if (IsDateTimeType(left) && IsDateTimeType(right))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a type is a numeric type.
    /// </summary>
    private static bool IsNumericType(WitSqlType type)
    {
        return type is WitSqlType.Integer or WitSqlType.Real or WitSqlType.Decimal;
    }

    /// <summary>
    /// Checks if a type is a date/time type.
    /// </summary>
    private static bool IsDateTimeType(WitSqlType type)
    {
        return type is WitSqlType.DateTime 
            or WitSqlType.DateOnly 
            or WitSqlType.TimeOnly 
            or WitSqlType.DateTimeOffset;
    }
}

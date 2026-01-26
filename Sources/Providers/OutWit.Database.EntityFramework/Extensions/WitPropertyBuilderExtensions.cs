using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OutWit.Database.EntityFramework.Extensions;

/// <summary>
/// Extension methods for <see cref="PropertyBuilder"/> to configure WitDatabase-specific features.
/// </summary>
public static class WitPropertyBuilderExtensions
{
    #region Row Version

    /// <summary>
    /// Configures the property as a row version for optimistic concurrency.
    /// WitDatabase uses an integer-based version that auto-increments on each update.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder<TProperty> IsWitRowVersion<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder)
    {
        return propertyBuilder
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValue(1);
    }

    /// <summary>
    /// Configures the property as a row version for optimistic concurrency.
    /// WitDatabase uses an integer-based version that auto-increments on each update.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder IsWitRowVersion(this PropertyBuilder propertyBuilder)
    {
        return propertyBuilder
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValue(1);
    }

    #endregion

    #region Computed Columns

    /// <summary>
    /// Configures the property as a computed column with the specified SQL expression.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <param name="sql">The SQL expression for the computed column.</param>
    /// <param name="stored">Whether the computed value is stored (persisted) or virtual.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder<TProperty> HasWitComputedColumnSql<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        string sql,
        bool stored = false)
    {
        return propertyBuilder.HasComputedColumnSql(sql, stored);
    }

    /// <summary>
    /// Configures the property as a computed column with the specified SQL expression.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <param name="sql">The SQL expression for the computed column.</param>
    /// <param name="stored">Whether the computed value is stored (persisted) or virtual.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder HasWitComputedColumnSql(
        this PropertyBuilder propertyBuilder,
        string sql,
        bool stored = false)
    {
        return propertyBuilder.HasComputedColumnSql(sql, stored);
    }

    #endregion

    #region JSON Columns

    /// <summary>
    /// Configures the property to be stored as a JSON column.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder<TProperty> HasJsonColumnType<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder)
    {
        return propertyBuilder.HasColumnType("JSON");
    }

    /// <summary>
    /// Configures the property to be stored as a JSON column.
    /// </summary>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder HasJsonColumnType(this PropertyBuilder propertyBuilder)
    {
        return propertyBuilder.HasColumnType("JSON");
    }

    #endregion

    #region Enum as String

    /// <summary>
    /// Configures the enum property to be stored as a string (TEXT).
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The same property builder for method chaining.</returns>
    public static PropertyBuilder<TEnum> HasEnumToStringConversion<TEnum>(
        this PropertyBuilder<TEnum> propertyBuilder)
        where TEnum : struct, Enum
    {
        return propertyBuilder
            .HasColumnType("TEXT")
            .HasConversion<string>();
    }

    #endregion
}

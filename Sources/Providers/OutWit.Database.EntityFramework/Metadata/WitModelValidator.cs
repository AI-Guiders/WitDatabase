using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace OutWit.Database.EntityFramework.Metadata;

/// <summary>
/// Validates an <see cref="IModel"/> for WitDatabase compatibility.
/// </summary>
public sealed class WitModelValidator : RelationalModelValidator
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitModelValidator"/> class.
    /// </summary>
    /// <param name="dependencies">The model validator dependencies.</param>
    /// <param name="relationalDependencies">The relational model validator dependencies.</param>
    public WitModelValidator(
        ModelValidatorDependencies dependencies,
        RelationalModelValidatorDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    #endregion

    #region Validation

    /// <inheritdoc/>
    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        ValidateSchemas(model, logger);
        ValidateKeyTypes(model, logger);
        ValidatePropertyTypes(model, logger);
    }

    private static void ValidateSchemas(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        // WitDatabase supports a default "public" schema
        // Custom schemas other than "public" or empty are not supported
        foreach (var entityType in model.GetEntityTypes())
        {
            var schema = entityType.GetSchema();
            if (!string.IsNullOrEmpty(schema) &&
                !schema.Equals("public", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"WitDatabase only supports the default 'public' schema. " +
                    $"Entity '{entityType.DisplayName()}' is mapped to schema '{schema}'. " +
                    $"Remove the schema specification or use 'public'.");
            }
        }
    }

    private static void ValidateKeyTypes(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null)
            {
                continue;
            }

            // Validate composite key length
            if (primaryKey.Properties.Count > 16)
            {
                throw new InvalidOperationException(
                    $"WitDatabase supports a maximum of 16 columns in a composite primary key. " +
                    $"Entity '{entityType.DisplayName()}' has {primaryKey.Properties.Count} key columns.");
            }

            // Validate key property types
            foreach (var property in primaryKey.Properties)
            {
                var clrType = property.ClrType;
                var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

                if (!IsSupportedKeyType(underlyingType))
                {
                    throw new InvalidOperationException(
                        $"WitDatabase does not support '{underlyingType.Name}' as a primary key type. " +
                        $"Property '{property.Name}' on entity '{entityType.DisplayName()}' uses an unsupported key type.");
                }
            }
        }
    }

    private static void ValidatePropertyTypes(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

                // Validate that complex types have value converters
                if (IsComplexType(underlyingType) && property.GetValueConverter() == null)
                {
                    // Only warn, don't throw - let EF Core handle it
                    // The warning can be logged if needed
                }
            }
        }
    }

    #endregion

    #region Helpers

    private static bool IsSupportedKeyType(Type type)
    {
        return type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(short) ||
               type == typeof(byte) ||
               type == typeof(uint) ||
               type == typeof(ulong) ||
               type == typeof(ushort) ||
               type == typeof(sbyte) ||
               type == typeof(Guid) ||
               type == typeof(string);
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        if (type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeOnly) ||
            type == typeof(TimeSpan) ||
            type == typeof(Guid) ||
            type == typeof(byte[]))
        {
            return false;
        }

        return true;
    }

    #endregion
}

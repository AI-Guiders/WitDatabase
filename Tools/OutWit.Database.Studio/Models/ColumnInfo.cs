using OutWit.Common.Abstract;
using OutWit.Common.Values;

namespace OutWit.Database.Studio.Models;

/// <summary>
/// Information about a table column.
/// </summary>
public sealed class ColumnInfo : ModelBase
{
    #region Model Base

    public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
    {
        if (modelBase is not ColumnInfo other)
            return false;

        return Name.Is(other.Name)
               && OrdinalPosition.Is(other.OrdinalPosition)
               && DataType.Is(other.DataType)
               && MaxLength.Is(other.MaxLength)
               && NumericPrecision.Is(other.NumericPrecision)
               && NumericScale.Is(other.NumericScale)
               && IsNullable.Is(other.IsNullable)
               && IsPrimaryKey.Is(other.IsPrimaryKey)
               && IsAutoIncrement.Is(other.IsAutoIncrement)
               && IsUnique.Is(other.IsUnique)
               && DefaultValue.Is(other.DefaultValue)
               && CheckExpression.Is(other.CheckExpression)
               && Collation.Is(other.Collation)
               && GenerationExpression.Is(other.GenerationExpression)
               && IsGenerated.Is(other.IsGenerated);
    }

    public override ColumnInfo Clone()
    {
        return new ColumnInfo
        {
            Name = Name,
            OrdinalPosition = OrdinalPosition,
            DataType = DataType,
            MaxLength = MaxLength,
            NumericPrecision = NumericPrecision,
            NumericScale = NumericScale,
            IsNullable = IsNullable,
            IsPrimaryKey = IsPrimaryKey,
            IsAutoIncrement = IsAutoIncrement,
            IsUnique = IsUnique,
            DefaultValue = DefaultValue,
            CheckExpression = CheckExpression,
            Collation = Collation,
            GenerationExpression = GenerationExpression,
            IsGenerated = IsGenerated
        };
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column position (1-based).
    /// </summary>
    public int OrdinalPosition { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum length for string/binary types.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the numeric precision for decimal types.
    /// </summary>
    public int? NumericPrecision { get; set; }

    /// <summary>
    /// Gets or sets the numeric scale for decimal types.
    /// </summary>
    public int? NumericScale { get; set; }

    /// <summary>
    /// Gets or sets whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets whether the column is auto-increment.
    /// </summary>
    public bool IsAutoIncrement { get; set; }

    /// <summary>
    /// Gets or sets whether the column has a UNIQUE constraint.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets the default value expression.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the CHECK constraint expression.
    /// </summary>
    public string? CheckExpression { get; set; }

    /// <summary>
    /// Gets or sets the collation name.
    /// </summary>
    public string? Collation { get; set; }

    /// <summary>
    /// Gets or sets the generation expression for computed columns.
    /// </summary>
    public string? GenerationExpression { get; set; }

    /// <summary>
    /// Gets or sets whether the column is generated (STORED, VIRTUAL, or NEVER).
    /// </summary>
    public string? IsGenerated { get; set; }

    /// <summary>
    /// Gets whether this is a computed column.
    /// </summary>
    public bool IsComputed => IsGenerated != null && IsGenerated != "NEVER";

    #endregion
}

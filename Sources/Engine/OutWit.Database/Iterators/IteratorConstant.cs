using OutWit.Database.Interfaces;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Iterators;

/// <summary>
/// Iterator that returns a single constant value.
/// Used for optimized queries like SELECT COUNT(*) FROM table (without WHERE)
/// where the result is known without scanning.
/// </summary>
internal sealed class IteratorConstant : IteratorBase
{
    #region Fields

    private readonly WitSqlRow m_row;
    private readonly IReadOnlyList<WitSqlColumnInfo> m_schema;
    private bool m_returned;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an iterator that returns a single row with a single integer value.
    /// </summary>
    /// <param name="value">The constant value to return.</param>
    /// <param name="columnName">The column name for the result.</param>
    public IteratorConstant(long value, string columnName = "COUNT")
    {
        m_row = new WitSqlRow(
            [WitSqlValue.FromInt(value)],
            [columnName]);
        m_schema = [new WitSqlColumnInfo { Name = columnName, Type = WitSqlType.Integer }];
    }

    /// <summary>
    /// Creates an iterator that returns a single row with a single WitSqlValue.
    /// Used for MIN/MAX optimization where the value type depends on the column.
    /// </summary>
    /// <param name="value">The constant value to return.</param>
    /// <param name="columnName">The column name for the result.</param>
    public IteratorConstant(WitSqlValue value, string columnName)
    {
        m_row = new WitSqlRow([value], [columnName]);
        m_schema = [new WitSqlColumnInfo { Name = columnName, Type = value.GetSqlType() }];
    }

    /// <summary>
    /// Creates an iterator that returns a single row with multiple values.
    /// </summary>
    /// <param name="values">The values to return.</param>
    /// <param name="schema">The schema for the result.</param>
    public IteratorConstant(WitSqlValue[] values, IReadOnlyList<WitSqlColumnInfo> schema)
    {
        var names = schema.Select(c => c.Name).ToArray();
        m_row = new WitSqlRow(values, names);
        m_schema = schema;
    }

    #endregion

    #region IResultIterator

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_returned = false;
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        if (m_returned)
            return false;

        m_returned = true;
        return true;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_returned = false;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Nothing to dispose
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema => m_schema;

    /// <inheritdoc/>
    public override WitSqlRow Current => m_row;

    /// <inheritdoc/>
    public override long EstimatedRowCount => 1;

    #endregion
}

/// <summary>
/// Iterator that returns a single NULL value.
/// Used for MIN/MAX on empty tables.
/// </summary>
internal sealed class IteratorConstantNull : IteratorBase
{
    #region Fields

    private readonly WitSqlRow m_row;
    private readonly IReadOnlyList<WitSqlColumnInfo> m_schema;
    private bool m_returned;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an iterator that returns a single row with a NULL value.
    /// </summary>
    /// <param name="columnName">The column name for the result.</param>
    public IteratorConstantNull(string columnName)
    {
        m_row = new WitSqlRow([WitSqlValue.Null], [columnName]);
        m_schema = [new WitSqlColumnInfo { Name = columnName, Type = WitSqlType.Null, IsNullable = true }];
    }

    #endregion

    #region IResultIterator

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_returned = false;
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        if (m_returned)
            return false;

        m_returned = true;
        return true;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_returned = false;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Nothing to dispose
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema => m_schema;

    /// <inheritdoc/>
    public override WitSqlRow Current => m_row;

    /// <inheritdoc/>
    public override long EstimatedRowCount => 1;

    #endregion
}

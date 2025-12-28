using OutWit.Database.Interfaces;
using OutWit.Database.Model;
using OutWit.Database.Validators;

namespace OutWit.Database.Iterators;

/// <summary>
/// Iterator for UNION operations.
/// Returns all rows from both sources, optionally removing duplicates.
/// </summary>
public sealed class IteratorUnion : IteratorBase
{
    #region Fields

    private readonly IResultIterator m_left;
    private readonly IResultIterator m_right;
    private readonly bool m_isAll;
    private HashSet<RowKey>? m_seenRows;
    private bool m_leftExhausted;
    private WitSqlRow m_current;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new UNION iterator.
    /// </summary>
    /// <param name="left">The left iterator.</param>
    /// <param name="right">The right iterator.</param>
    /// <param name="isAll">If true, keeps duplicates (UNION ALL); if false, removes duplicates (UNION).</param>
    public IteratorUnion(IResultIterator left, IResultIterator right, bool isAll)
    {
        m_left = left;
        m_right = right;
        m_isAll = isAll;
        
        ValidatorSetOperationSchema.ValidateSchemaCompatibility(left.Schema, right.Schema, "UNION");
    }

    #endregion
    
    #region IResultIterator

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_left.Open();
        m_right.Open();
        m_leftExhausted = false;

        if (!m_isAll)
        {
            m_seenRows = [];
        }
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        while (true)
        {
            WitSqlRow row;

            if (!m_leftExhausted)
            {
                if (m_left.MoveNext())
                {
                    row = m_left.Current;
                }
                else
                {
                    m_leftExhausted = true;
                    continue;
                }
            }
            else
            {
                if (!m_right.MoveNext())
                    return false;

                row = m_right.Current;
            }

            // For UNION ALL, return all rows
            if (m_isAll)
            {
                m_current = row;
                return true;
            }

            // For UNION, skip duplicates
            var key = new RowKey(row);
            if (m_seenRows!.Add(key))
            {
                m_current = row;
                return true;
            }
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_left.Reset();
        m_right.Reset();
        m_leftExhausted = false;
        m_seenRows?.Clear();
        m_current = default;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public override void Dispose()
    {
        m_left.Dispose();
        m_right.Dispose();
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema => m_left.Schema;

    /// <inheritdoc/>
    public override WitSqlRow Current => m_current;

    #endregion
}

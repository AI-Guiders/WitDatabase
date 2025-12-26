using OutWit.Database.Interfaces;
using OutWit.Database.Model;

namespace OutWit.Database.Iterators;

/// <summary>
/// Iterator for INTERSECT operations.
/// Returns rows that exist in both sources.
/// </summary>
public sealed class IteratorIntersect : IteratorBase
{
    #region Fields

    private readonly IResultIterator m_left;
    private readonly IResultIterator m_right;
    private readonly bool m_isAll;
    private HashSet<RowKey>? m_rightRows;
    private Dictionary<RowKey, int>? m_rightRowCounts;
    private WitSqlRow m_current;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new INTERSECT iterator.
    /// </summary>
    /// <param name="left">The left iterator.</param>
    /// <param name="right">The right iterator.</param>
    /// <param name="isAll">If true, preserves duplicates (INTERSECT ALL); if false, removes duplicates.</param>
    public IteratorIntersect(IResultIterator left, IResultIterator right, bool isAll)
    {
        m_left = left;
        m_right = right;
        m_isAll = isAll;
    }

    #endregion

    #region IResultIterator

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema => m_left.Schema;

    /// <inheritdoc/>
    public override WitSqlRow Current => m_current;

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_left.Open();
        m_right.Open();

        // Buffer all right rows
        if (m_isAll)
        {
            // For INTERSECT ALL, track counts
            m_rightRowCounts = [];
            while (m_right.MoveNext())
            {
                var key = new RowKey(m_right.Current);
                m_rightRowCounts.TryGetValue(key, out var count);
                m_rightRowCounts[key] = count + 1;
            }
        }
        else
        {
            // For INTERSECT, just track existence
            m_rightRows = [];
            while (m_right.MoveNext())
            {
                m_rightRows.Add(new RowKey(m_right.Current));
            }
        }
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        while (m_left.MoveNext())
        {
            var key = new RowKey(m_left.Current);

            if (m_isAll)
            {
                // For INTERSECT ALL, decrement count and return if > 0
                if (m_rightRowCounts!.TryGetValue(key, out var count) && count > 0)
                {
                    m_rightRowCounts[key] = count - 1;
                    m_current = m_left.Current;
                    return true;
                }
            }
            else
            {
                // For INTERSECT, check existence and remove to avoid duplicates
                if (m_rightRows!.Remove(key))
                {
                    m_current = m_left.Current;
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_left.Reset();
        m_right.Reset();
        m_rightRows?.Clear();
        m_rightRowCounts?.Clear();
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
}

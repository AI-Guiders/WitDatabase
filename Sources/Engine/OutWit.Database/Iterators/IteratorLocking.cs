using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Interfaces;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Schema;
using OutWit.Database.Sql;

namespace OutWit.Database.Iterators;

/// <summary>
/// Iterator that applies row-level locks (FOR UPDATE / FOR SHARE) to rows.
/// Wraps another iterator and acquires locks on each row as it's read.
/// Requires an active MVCC transaction.
/// </summary>
internal sealed class IteratorLocking : IteratorBase
{
    #region Fields

    private readonly IResultIterator m_source;
    private readonly ClauseFor m_forClause;
    private readonly IMvccTransaction m_transaction;
    private readonly string m_tableName;
    private readonly RowLockMode m_lockMode;
    private readonly RowLockWaitMode m_waitMode;
    private WitSqlRow m_current;
    private bool m_skippedRow;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new locking iterator.
    /// </summary>
    /// <param name="source">The source iterator providing rows.</param>
    /// <param name="forClause">The FOR clause specifying lock type and options.</param>
    /// <param name="transaction">The MVCC transaction to acquire locks in.</param>
    /// <param name="tableName">The table name for constructing lock keys.</param>
    public IteratorLocking(
        IResultIterator source, 
        ClauseFor forClause, 
        IMvccTransaction transaction,
        string tableName)
    {
        m_source = source;
        m_forClause = forClause;
        m_transaction = transaction;
        m_tableName = tableName;
        m_lockMode = MapLockMode(forClause.LockingType);
        m_waitMode = MapWaitMode(forClause);
    }

    #endregion

    #region IResultIterator

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_source.Open();
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        while (m_source.MoveNext())
        {
            var row = m_source.Current;
            
            // Get row ID for locking
            var rowIdValue = row["_rowid"];
            if (rowIdValue.IsNull)
            {
                // No row ID - can't lock, just return row
                m_current = row;
                m_skippedRow = false;
                return true;
            }

            var rowId = rowIdValue.AsInt64();
            var lockKey = SchemaCatalog.CreateRowKey(m_tableName, rowId);

            // Try to acquire lock
            byte[]? lockedValue;
            try
            {
                if (m_lockMode == RowLockMode.Exclusive)
                {
                    lockedValue = m_transaction.GetForUpdate(lockKey, m_waitMode);
                }
                else
                {
                    lockedValue = m_transaction.GetForShare(lockKey, m_waitMode);
                }
            }
            catch (Core.Exceptions.RowLockException)
            {
                // NOWAIT mode - lock couldn't be acquired
                throw new InvalidOperationException(
                    $"Could not obtain lock on row {rowId} in table '{m_tableName}'. " +
                    "The row is locked by another transaction.");
            }

            // Handle SKIP LOCKED - if lock couldn't be acquired, skip this row
            if (lockedValue == null && m_waitMode == RowLockWaitMode.SkipLocked)
            {
                m_skippedRow = true;
                continue; // Skip to next row
            }

            m_current = row;
            m_skippedRow = false;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_source.Reset();
        m_current = default;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public override void Dispose()
    {
        m_source.Dispose();
    }

    #endregion

    #region Helpers

    private static RowLockMode MapLockMode(LockingType lockingType)
    {
        return lockingType switch
        {
            LockingType.ForUpdate => RowLockMode.Exclusive,
            LockingType.ForShare => RowLockMode.Shared,
            _ => throw new ArgumentOutOfRangeException(nameof(lockingType))
        };
    }

    private static RowLockWaitMode MapWaitMode(ClauseFor forClause)
    {
        if (forClause.IsNoWait)
            return RowLockWaitMode.NoWait;
        if (forClause.IsSkipLocked)
            return RowLockWaitMode.SkipLocked;
        return RowLockWaitMode.Wait;
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema => m_source.Schema;

    /// <inheritdoc/>
    public override WitSqlRow Current => m_current;

    /// <summary>
    /// Gets whether the last row was skipped due to SKIP LOCKED.
    /// </summary>
    public bool SkippedRow => m_skippedRow;

    #endregion
}

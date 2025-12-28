using OutWit.Database.Core.Interfaces;
using OutWit.Database.Transactions;

namespace OutWit.Database.Engine;

/// <summary>
/// Transaction management operations for WitSqlEngine.
/// </summary>
public sealed partial class WitSqlEngine
{
    #region Transaction Control

    /// <summary>
    /// Begin a new transaction with default isolation level.
    /// </summary>
    /// <returns>A disposable handle that will auto-rollback if not committed.</returns>
    public IDisposable BeginTransaction()
    {
        return BeginTransaction(IsolationLevel.ReadCommitted);
    }

    /// <summary>
    /// Begin a new transaction with specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <returns>A disposable handle that will auto-rollback if not committed.</returns>
    public IDisposable BeginTransaction(IsolationLevel isolationLevel)
    {
        if (m_currentTransaction != null)
            throw new InvalidOperationException("A transaction is already active. Commit or rollback it first.");

        m_currentTransaction = m_database.BeginTransaction(isolationLevel);
        return new TransactionHandle(this);
    }

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    public void Commit()
    {
        if (m_currentTransaction == null)
            return;

        m_currentTransaction.Commit();
        m_currentTransaction.Dispose();
        m_currentTransaction = null;
    }

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    public void Rollback()
    {
        if (m_currentTransaction == null)
            return;

        m_currentTransaction.Rollback();
        m_currentTransaction.Dispose();
        m_currentTransaction = null;
    }

    #endregion

    #region Savepoints

    /// <summary>
    /// Create a savepoint within the current transaction.
    /// </summary>
    /// <param name="name">The savepoint name.</param>
    public void CreateSavepoint(string name)
    {
        if (m_currentTransaction == null)
            throw new InvalidOperationException("No active transaction. Begin a transaction first.");

        if (m_currentTransaction is ITransactionWithSavepoints txWithSavepoints)
        {
            txWithSavepoints.CreateSavepoint(name);
        }
        else
        {
            throw new NotSupportedException("Current transaction does not support savepoints.");
        }
    }

    /// <summary>
    /// Release a savepoint within the current transaction.
    /// </summary>
    /// <param name="name">The savepoint name.</param>
    public void ReleaseSavepoint(string name)
    {
        if (m_currentTransaction == null)
            throw new InvalidOperationException("No active transaction.");

        if (m_currentTransaction is ITransactionWithSavepoints txWithSavepoints)
        {
            txWithSavepoints.ReleaseSavepoint(name);
        }
        else
        {
            throw new NotSupportedException("Current transaction does not support savepoints.");
        }
    }

    /// <summary>
    /// Rollback to a savepoint within the current transaction.
    /// </summary>
    /// <param name="name">The savepoint name.</param>
    public void RollbackToSavepoint(string name)
    {
        if (m_currentTransaction == null)
            throw new InvalidOperationException("No active transaction.");

        if (m_currentTransaction is ITransactionWithSavepoints txWithSavepoints)
        {
            txWithSavepoints.RollbackToSavepoint(name);
        }
        else
        {
            throw new NotSupportedException("Current transaction does not support savepoints.");
        }
    }

    #endregion
}

using System.Data;
using System.Data.Common;

namespace OutWit.Database.AdoNet;

/// <summary>
/// Represents a transaction to be performed at a WitDatabase database.
/// </summary>
public sealed class WitDbTransaction : DbTransaction
{
    #region Fields

    private WitDbConnection? m_connection;
    private readonly IsolationLevel m_isolationLevel;
    private bool m_completed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbTransaction"/> class.
    /// </summary>
    /// <param name="connection">The connection associated with this transaction.</param>
    /// <param name="isolationLevel">The isolation level for this transaction.</param>
    internal WitDbTransaction(WitDbConnection connection, IsolationLevel isolationLevel)
    {
        m_connection = connection;
        m_isolationLevel = isolationLevel == IsolationLevel.Unspecified 
            ? IsolationLevel.ReadCommitted 
            : isolationLevel;
    }

    #endregion

    #region Commit/Rollback

    /// <inheritdoc/>
    public override void Commit()
    {
        EnsureNotCompleted();
        EnsureConnectionOpen();

        try
        {
            m_connection!.Engine!.Commit();
            m_completed = true;
        }
        finally
        {
            m_connection!.ClearTransaction();
        }
    }

    /// <summary>
    /// Commits the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(Commit, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Rollback()
    {
        EnsureNotCompleted();

        try
        {
            if (m_connection?.Engine != null)
            {
                m_connection.Engine.Rollback();
            }
            m_completed = true;
        }
        finally
        {
            m_connection?.ClearTransaction();
        }
    }

    /// <summary>
    /// Rolls back the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(Rollback, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Savepoints

    /// <summary>
    /// Creates a savepoint in the transaction.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    public void Save(string savepointName)
    {
        EnsureNotCompleted();
        EnsureConnectionOpen();

        if (string.IsNullOrEmpty(savepointName))
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));

        m_connection!.Engine!.CreateSavepoint(savepointName);
    }

    /// <summary>
    /// Creates a savepoint in the transaction asynchronously.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Save(savepointName), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rolls back to a savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    public void Rollback(string savepointName)
    {
        EnsureNotCompleted();
        EnsureConnectionOpen();

        if (string.IsNullOrEmpty(savepointName))
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));

        m_connection!.Engine!.RollbackToSavepoint(savepointName);
    }

    /// <summary>
    /// Rolls back to a savepoint asynchronously.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Rollback(savepointName), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases a savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    public void Release(string savepointName)
    {
        EnsureNotCompleted();
        EnsureConnectionOpen();

        if (string.IsNullOrEmpty(savepointName))
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));

        m_connection!.Engine!.ReleaseSavepoint(savepointName);
    }

    /// <summary>
    /// Releases a savepoint asynchronously.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Release(savepointName), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Helpers

    private void EnsureNotCompleted()
    {
        if (m_completed)
            throw new InvalidOperationException("This transaction has already been committed or rolled back.");
    }

    private void EnsureConnectionOpen()
    {
        if (m_connection == null || m_connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection is not open.");
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !m_completed && m_connection != null)
        {
            try
            {
                Rollback();
            }
            catch
            {
                // Ignore errors during dispose rollback
            }
        }

        m_connection = null;
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!m_completed && m_connection != null)
        {
            try
            {
                await RollbackAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during dispose rollback
            }
        }

        m_connection = null;
        await base.DisposeAsync().ConfigureAwait(false);
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IsolationLevel IsolationLevel => m_isolationLevel;

    /// <inheritdoc/>
    protected override DbConnection? DbConnection => m_connection;

    /// <summary>
    /// Gets the connection associated with this transaction.
    /// </summary>
    public new WitDbConnection? Connection => m_connection;

    #endregion
}

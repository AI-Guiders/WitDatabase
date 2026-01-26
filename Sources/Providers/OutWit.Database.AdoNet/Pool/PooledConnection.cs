using System.Data;

namespace OutWit.Database.AdoNet.Pool;

/// <summary>
/// Represents a pooled connection wrapper.
/// </summary>
internal sealed class PooledConnection : IDisposable
{
    #region Fields

    private readonly WitDbConnection m_innerConnection;
    private readonly ConnectionPool m_pool;
    private readonly DateTime m_createdAt;
    private DateTime m_lastUsedAt;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new pooled connection.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="pool">The pool that owns this connection.</param>
    public PooledConnection(string connectionString, ConnectionPool pool)
    {
        m_innerConnection = new WitDbConnection(connectionString);
        m_pool = pool;
        m_createdAt = DateTime.UtcNow;
        m_lastUsedAt = m_createdAt;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Opens the connection if it's not already open.
    /// </summary>
    public void Open()
    {
        if (m_innerConnection.State != ConnectionState.Open)
        {
            m_innerConnection.Open();
        }
        m_lastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Opens the connection asynchronously if it's not already open.
    /// </summary>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (m_innerConnection.State != ConnectionState.Open)
        {
            await m_innerConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        m_lastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates that the connection is still usable.
    /// </summary>
    /// <returns>True if the connection is valid.</returns>
    public bool Validate()
    {
        try
        {
            if (m_innerConnection.State != ConnectionState.Open)
                return false;

            // Simple validation - try to execute a trivial query
            using var cmd = m_innerConnection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the connection has exceeded its lifetime.
    /// </summary>
    /// <param name="maxLifetimeSeconds">Maximum lifetime in seconds. 0 means unlimited.</param>
    /// <returns>True if the connection has expired.</returns>
    public bool IsExpired(int maxLifetimeSeconds)
    {
        if (maxLifetimeSeconds <= 0)
            return false;

        return (DateTime.UtcNow - m_createdAt).TotalSeconds > maxLifetimeSeconds;
    }

    /// <summary>
    /// Checks if the connection has been idle too long.
    /// </summary>
    /// <param name="idleTimeoutSeconds">Idle timeout in seconds. 0 means unlimited.</param>
    /// <returns>True if the connection has been idle too long.</returns>
    public bool IsIdle(int idleTimeoutSeconds)
    {
        if (idleTimeoutSeconds <= 0)
            return false;

        return (DateTime.UtcNow - m_lastUsedAt).TotalSeconds > idleTimeoutSeconds;
    }

    /// <summary>
    /// Marks the connection as recently used.
    /// </summary>
    public void Touch()
    {
        m_lastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Closes and disposes the inner connection.
    /// </summary>
    public void CloseInner()
    {
        m_innerConnection.Close();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (m_disposed)
            return;

        m_disposed = true;
        m_innerConnection.Dispose();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the inner connection.
    /// </summary>
    public WitDbConnection InnerConnection => m_innerConnection;

    /// <summary>
    /// Gets the time when this connection was created.
    /// </summary>
    public DateTime CreatedAt => m_createdAt;

    /// <summary>
    /// Gets the time when this connection was last used.
    /// </summary>
    public DateTime LastUsedAt => m_lastUsedAt;

    /// <summary>
    /// Gets whether this pooled connection has been disposed.
    /// </summary>
    public bool IsDisposed => m_disposed;

    #endregion
}

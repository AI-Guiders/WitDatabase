using System.Collections.Concurrent;

namespace OutWit.Database.AdoNet.Pool;

/// <summary>
/// Manages a pool of database connections for efficient reuse.
/// </summary>
public sealed class ConnectionPool : IDisposable
{
    #region Fields

    private static readonly ConcurrentDictionary<string, ConnectionPool> s_pools = new(StringComparer.OrdinalIgnoreCase);

    private readonly PoolOptions m_options;
    private readonly ConcurrentBag<PooledConnection> m_availableConnections;
    private readonly ConcurrentDictionary<int, PooledConnection> m_activeConnections;
    private readonly SemaphoreSlim m_semaphore;
    private readonly Timer? m_cleanupTimer;
    private readonly object m_lock = new();
    private int m_totalConnections;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new connection pool with the specified options.
    /// </summary>
    /// <param name="options">Pool configuration options.</param>
    private ConnectionPool(PoolOptions options)
    {
        m_options = options;
        m_availableConnections = new ConcurrentBag<PooledConnection>();
        m_activeConnections = new ConcurrentDictionary<int, PooledConnection>();
        m_semaphore = new SemaphoreSlim(options.MaxPoolSize, options.MaxPoolSize);

        // Start cleanup timer if idle timeout is configured
        if (options.IdleTimeout > 0)
        {
            var cleanupInterval = TimeSpan.FromSeconds(Math.Max(30, options.IdleTimeout / 2));
            m_cleanupTimer = new Timer(CleanupCallback, null, cleanupInterval, cleanupInterval);
        }

        // Pre-create minimum connections
        for (int i = 0; i < options.MinPoolSize; i++)
        {
            var conn = CreateNewConnection();
            m_availableConnections.Add(conn);
        }
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Gets or creates a pool for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection pool.</returns>
    public static ConnectionPool GetPool(string connectionString)
    {
        return s_pools.GetOrAdd(connectionString, cs =>
        {
            var builder = new WitDbConnectionStringBuilder(cs);
            var options = new PoolOptions
            {
                ConnectionString = cs,
                MinPoolSize = builder.MinPoolSize,
                MaxPoolSize = builder.MaxPoolSize
            };
            return new ConnectionPool(options);
        });
    }

    /// <summary>
    /// Gets or creates a pool with the specified options.
    /// </summary>
    /// <param name="options">Pool configuration options.</param>
    /// <returns>The connection pool.</returns>
    public static ConnectionPool GetPool(PoolOptions options)
    {
        return s_pools.GetOrAdd(options.ConnectionString, _ => new ConnectionPool(options));
    }

    /// <summary>
    /// Clears all pools.
    /// </summary>
    public static void ClearAllPools()
    {
        foreach (var pool in s_pools.Values)
        {
            pool.Clear();
        }
        s_pools.Clear();
    }

    /// <summary>
    /// Clears the pool for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    public static void ClearPool(string connectionString)
    {
        if (s_pools.TryRemove(connectionString, out var pool))
        {
            pool.Clear();
            pool.Dispose();
        }
    }

    #endregion

    #region Pool Operations

    /// <summary>
    /// Gets a connection from the pool.
    /// </summary>
    /// <returns>A pooled connection.</returns>
    /// <exception cref="InvalidOperationException">If the pool is exhausted and cannot create new connections.</exception>
    public WitDbConnection GetConnection()
    {
        EnsureNotDisposed();

        // Wait for available slot
        if (!m_semaphore.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new InvalidOperationException("Connection pool is exhausted. No connections available within timeout.");
        }

        try
        {
            // Try to get an existing connection
            while (m_availableConnections.TryTake(out var pooledConn))
            {
                // Check if connection is still valid
                if (pooledConn.IsDisposed || 
                    pooledConn.IsExpired(m_options.ConnectionLifetime) ||
                    (m_options.ValidateOnBorrow && !pooledConn.Validate()))
                {
                    // Discard invalid connection
                    Interlocked.Decrement(ref m_totalConnections);
                    pooledConn.Dispose();
                    continue;
                }

                // Found a valid connection
                pooledConn.Touch();
                m_activeConnections.TryAdd(pooledConn.GetHashCode(), pooledConn);
                return CreateWrapper(pooledConn);
            }

            // No available connections, create a new one
            var newConn = CreateNewConnection();
            newConn.Open();
            m_activeConnections.TryAdd(newConn.GetHashCode(), newConn);
            return CreateWrapper(newConn);
        }
        catch
        {
            m_semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Gets a connection from the pool asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pooled connection.</returns>
    public async Task<WitDbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        // Wait for available slot
        if (!await m_semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Connection pool is exhausted. No connections available within timeout.");
        }

        try
        {
            // Try to get an existing connection
            while (m_availableConnections.TryTake(out var pooledConn))
            {
                // Check if connection is still valid
                if (pooledConn.IsDisposed || 
                    pooledConn.IsExpired(m_options.ConnectionLifetime) ||
                    (m_options.ValidateOnBorrow && !pooledConn.Validate()))
                {
                    // Discard invalid connection
                    Interlocked.Decrement(ref m_totalConnections);
                    pooledConn.Dispose();
                    continue;
                }

                // Found a valid connection
                pooledConn.Touch();
                m_activeConnections.TryAdd(pooledConn.GetHashCode(), pooledConn);
                return CreateWrapper(pooledConn);
            }

            // No available connections, create a new one
            var newConn = CreateNewConnection();
            await newConn.OpenAsync(cancellationToken).ConfigureAwait(false);
            m_activeConnections.TryAdd(newConn.GetHashCode(), newConn);
            return CreateWrapper(newConn);
        }
        catch
        {
            m_semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Returns a connection to the pool.
    /// </summary>
    /// <param name="pooledConnection">The pooled connection to return.</param>
    internal void ReturnConnection(PooledConnection pooledConnection)
    {
        if (m_disposed)
        {
            pooledConnection.Dispose();
            return;
        }

        m_activeConnections.TryRemove(pooledConnection.GetHashCode(), out _);

        // Check if connection is still valid
        if (pooledConnection.IsDisposed || pooledConnection.IsExpired(m_options.ConnectionLifetime))
        {
            Interlocked.Decrement(ref m_totalConnections);
            pooledConnection.Dispose();
        }
        else
        {
            pooledConnection.Touch();
            m_availableConnections.Add(pooledConnection);
        }

        m_semaphore.Release();
    }

    /// <summary>
    /// Clears all connections from the pool.
    /// </summary>
    public void Clear()
    {
        lock (m_lock)
        {
            // Dispose available connections
            while (m_availableConnections.TryTake(out var conn))
            {
                conn.Dispose();
            }

            // Dispose active connections
            foreach (var conn in m_activeConnections.Values)
            {
                conn.Dispose();
            }
            m_activeConnections.Clear();

            m_totalConnections = 0;
        }
    }

    #endregion

    #region Private Methods

    private PooledConnection CreateNewConnection()
    {
        Interlocked.Increment(ref m_totalConnections);
        return new PooledConnection(m_options.ConnectionString, this);
    }

    private WitDbConnection CreateWrapper(PooledConnection pooledConnection)
    {
        // For now, return the inner connection directly
        // In a full implementation, we'd wrap it in a PooledConnectionWrapper
        // that returns it to the pool on dispose
        return pooledConnection.InnerConnection;
    }

    private void CleanupCallback(object? state)
    {
        if (m_disposed)
            return;

        var toRemove = new List<PooledConnection>();

        // Check available connections for idle timeout
        var tempList = new List<PooledConnection>();
        while (m_availableConnections.TryTake(out var conn))
        {
            if (conn.IsIdle(m_options.IdleTimeout) && m_totalConnections > m_options.MinPoolSize)
            {
                toRemove.Add(conn);
            }
            else
            {
                tempList.Add(conn);
            }
        }

        // Put back valid connections
        foreach (var conn in tempList)
        {
            m_availableConnections.Add(conn);
        }

        // Dispose idle connections
        foreach (var conn in toRemove)
        {
            Interlocked.Decrement(ref m_totalConnections);
            conn.Dispose();
        }
    }

    private void EnsureNotDisposed()
    {
        if (m_disposed)
            throw new ObjectDisposedException(nameof(ConnectionPool));
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (m_disposed)
            return;

        m_disposed = true;
        m_cleanupTimer?.Dispose();
        Clear();
        m_semaphore.Dispose();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total number of connections (active + available).
    /// </summary>
    public int TotalConnections => m_totalConnections;

    /// <summary>
    /// Gets the number of available connections in the pool.
    /// </summary>
    public int AvailableConnections => m_availableConnections.Count;

    /// <summary>
    /// Gets the number of active connections currently in use.
    /// </summary>
    public int ActiveConnections => m_activeConnections.Count;

    /// <summary>
    /// Gets the pool options.
    /// </summary>
    public PoolOptions Options => m_options;

    #endregion
}

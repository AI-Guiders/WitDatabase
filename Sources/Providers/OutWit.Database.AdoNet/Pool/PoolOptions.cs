namespace OutWit.Database.AdoNet.Pool;

/// <summary>
/// Configuration options for connection pooling.
/// </summary>
public sealed class PoolOptions
{
    #region Constants

    /// <summary>
    /// Default minimum pool size.
    /// </summary>
    public const int DEFAULT_MIN_POOL_SIZE = 0;

    /// <summary>
    /// Default maximum pool size.
    /// </summary>
    public const int DEFAULT_MAX_POOL_SIZE = 100;

    /// <summary>
    /// Default connection lifetime in seconds (0 = unlimited).
    /// </summary>
    public const int DEFAULT_CONNECTION_LIFETIME = 0;

    /// <summary>
    /// Default idle timeout in seconds (0 = unlimited).
    /// </summary>
    public const int DEFAULT_IDLE_TIMEOUT = 300;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the minimum number of connections in the pool.
    /// </summary>
    public int MinPoolSize { get; set; } = DEFAULT_MIN_POOL_SIZE;

    /// <summary>
    /// Gets or sets the maximum number of connections in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = DEFAULT_MAX_POOL_SIZE;

    /// <summary>
    /// Gets or sets the maximum lifetime of a connection in seconds.
    /// 0 means unlimited lifetime.
    /// </summary>
    public int ConnectionLifetime { get; set; } = DEFAULT_CONNECTION_LIFETIME;

    /// <summary>
    /// Gets or sets the idle timeout in seconds before a connection is closed.
    /// 0 means connections are never closed due to idle time.
    /// </summary>
    public int IdleTimeout { get; set; } = DEFAULT_IDLE_TIMEOUT;

    /// <summary>
    /// Gets or sets whether to validate connections before returning them from the pool.
    /// </summary>
    public bool ValidateOnBorrow { get; set; }

    /// <summary>
    /// Gets or sets the connection string for creating new connections.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    #endregion
}

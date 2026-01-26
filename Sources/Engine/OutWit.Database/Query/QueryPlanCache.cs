using System.Collections.Concurrent;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Query;

/// <summary>
/// Cache entry for a compiled query plan.
/// </summary>
public sealed class QueryPlanCacheEntry
{
    /// <summary>
    /// The normalized SQL key.
    /// </summary>
    public required string NormalizedSql { get; init; }

    /// <summary>
    /// The parsed statement (can be reused).
    /// </summary>
    public required WitSqlStatement Statement { get; init; }

    /// <summary>
    /// The last time this entry was accessed.
    /// </summary>
    public DateTime LastAccess { get; set; }

    /// <summary>
    /// Number of times this entry has been used.
    /// </summary>
    public long HitCount;
}

/// <summary>
/// LRU cache for query plans to avoid repeated parsing and planning.
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public sealed class QueryPlanCache
{
    #region Constants

    /// <summary>
    /// Default maximum number of cached plans.
    /// </summary>
    private const int DEFAULT_MAX_SIZE = 1000;

    /// <summary>
    /// Default time-to-live for cached plans (30 minutes).
    /// </summary>
    private static readonly TimeSpan DEFAULT_TTL = TimeSpan.FromMinutes(30);

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<string, QueryPlanCacheEntry> m_cache;
    private readonly int m_maxSize;
    private readonly TimeSpan m_ttl;
    private long m_hits;
    private long m_misses;
    private readonly object m_evictionLock = new();

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new query plan cache with default settings.
    /// </summary>
    public QueryPlanCache() : this(DEFAULT_MAX_SIZE, DEFAULT_TTL)
    {
    }

    /// <summary>
    /// Creates a new query plan cache with specified settings.
    /// </summary>
    /// <param name="maxSize">Maximum number of cached plans.</param>
    /// <param name="ttl">Time-to-live for cached plans.</param>
    public QueryPlanCache(int maxSize, TimeSpan ttl)
    {
        m_maxSize = maxSize;
        m_ttl = ttl;
        m_cache = new ConcurrentDictionary<string, QueryPlanCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current number of cached plans.
    /// </summary>
    public int Count => m_cache.Count;

    /// <summary>
    /// Gets the cache hit count.
    /// </summary>
    public long HitCount => Interlocked.Read(ref m_hits);

    /// <summary>
    /// Gets the cache miss count.
    /// </summary>
    public long MissCount => Interlocked.Read(ref m_misses);

    /// <summary>
    /// Gets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double HitRatio
    {
        get
        {
            var total = HitCount + MissCount;
            return total > 0 ? (double)HitCount / total : 0.0;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tries to get a cached plan for the given SQL.
    /// </summary>
    /// <param name="sql">The SQL query.</param>
    /// <param name="entry">The cached entry if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool TryGet(string sql, out QueryPlanCacheEntry? entry)
    {
        var key = NormalizeSql(sql);

        if (m_cache.TryGetValue(key, out entry))
        {
            // Check TTL
            if (DateTime.UtcNow - entry.LastAccess > m_ttl)
            {
                // Entry expired
                m_cache.TryRemove(key, out _);
                entry = null;
                Interlocked.Increment(ref m_misses);
                return false;
            }

            // Update access time and hit count
            entry.LastAccess = DateTime.UtcNow;
            Interlocked.Increment(ref entry.HitCount);
            Interlocked.Increment(ref m_hits);
            return true;
        }

        entry = null;
        Interlocked.Increment(ref m_misses);
        return false;
    }

    /// <summary>
    /// Adds a plan to the cache.
    /// </summary>
    /// <param name="sql">The SQL query.</param>
    /// <param name="statement">The parsed statement.</param>
    public void Add(string sql, WitSqlStatement statement)
    {
        var key = NormalizeSql(sql);

        // Check if we need to evict entries
        if (m_cache.Count >= m_maxSize)
        {
            EvictOldEntries();
        }

        var entry = new QueryPlanCacheEntry
        {
            NormalizedSql = key,
            Statement = statement,
            LastAccess = DateTime.UtcNow,
            HitCount = 0
        };

        m_cache.TryAdd(key, entry);
    }

    /// <summary>
    /// Invalidates all cached plans.
    /// Call this after DDL operations (CREATE TABLE, DROP INDEX, etc.).
    /// </summary>
    public void Invalidate()
    {
        m_cache.Clear();
    }

    /// <summary>
    /// Invalidates cached plans for a specific table.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    public void InvalidateTable(string tableName)
    {
        // Remove entries that reference this table
        // This is a simple implementation - for better accuracy,
        // we would need to track which tables each plan references
        var keysToRemove = m_cache
            .Where(kvp => ContainsTableReference(kvp.Value.NormalizedSql, tableName))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            m_cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Resets the cache statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref m_hits, 0);
        Interlocked.Exchange(ref m_misses, 0);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Normalizes SQL for cache key generation.
    /// Trims whitespace and normalizes spacing.
    /// </summary>
    private static string NormalizeSql(string sql)
    {
        // Simple normalization: trim and collapse multiple spaces
        // More sophisticated normalization could canonicalize parameter names,
        // but this is sufficient for most cases
        return string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Evicts old entries when cache is full.
    /// Uses a combination of LRU and hit count.
    /// </summary>
    private void EvictOldEntries()
    {
        lock (m_evictionLock)
        {
            // Only evict if still over limit (another thread might have evicted)
            if (m_cache.Count < m_maxSize)
                return;

            // Evict 20% of entries - prefer entries that are old AND rarely used
            var entriesToEvict = m_cache
                .OrderBy(e => e.Value.HitCount)
                .ThenBy(e => e.Value.LastAccess)
                .Take(m_maxSize / 5)
                .Select(e => e.Key)
                .ToList();

            foreach (var key in entriesToEvict)
            {
                m_cache.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Checks if SQL contains a reference to a table name.
    /// Simple string matching - could be improved with proper AST analysis.
    /// </summary>
    private static bool ContainsTableReference(string sql, string tableName)
    {
        // Case-insensitive search for table name as a word
        var sqlLower = sql.ToLowerInvariant();
        var tableNameLower = tableName.ToLowerInvariant();
        
        var index = sqlLower.IndexOf(tableNameLower, StringComparison.Ordinal);
        while (index >= 0)
        {
            // Check if it's a word boundary
            var before = index == 0 || !char.IsLetterOrDigit(sqlLower[index - 1]);
            var after = index + tableNameLower.Length >= sqlLower.Length 
                || !char.IsLetterOrDigit(sqlLower[index + tableNameLower.Length]);
            
            if (before && after)
                return true;
            
            index = sqlLower.IndexOf(tableNameLower, index + 1, StringComparison.Ordinal);
        }
        
        return false;
    }

    #endregion
}

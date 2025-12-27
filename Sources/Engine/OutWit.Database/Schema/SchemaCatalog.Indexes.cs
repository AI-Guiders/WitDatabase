using OutWit.Database.Definitions;

namespace OutWit.Database.Schema;

/// <summary>
/// Indexes management part of SchemaCatalog.
/// </summary>
public sealed partial class SchemaCatalog
{
    #region Indexes

    /// <summary>
    /// Gets an index definition by name.
    /// </summary>
    public DefinitionIndex? GetIndex(string name)
    {
        m_lock.EnterReadLock();
        try
        {
            m_indexes.TryGetValue(name, out var index);
            return index;
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all indexes.
    /// </summary>
    public IEnumerable<DefinitionIndex> GetIndexes()
    {
        m_lock.EnterReadLock();
        try
        {
            return m_indexes.Values.ToList();
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all indexes for a table.
    /// </summary>
    public IEnumerable<DefinitionIndex> GetTableIndexes(string tableName)
    {
        m_lock.EnterReadLock();
        try
        {
            return m_indexes.Values
                .Where(i => i.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Creates a new index.
    /// </summary>
    public void CreateIndex(DefinitionIndex index)
    {
        m_lock.EnterWriteLock();
        try
        {
            if (m_indexes.ContainsKey(index.Name))
                throw new InvalidOperationException($"Index '{index.Name}' already exists");

            if (!m_tables.ContainsKey(index.TableName))
                throw new InvalidOperationException($"Table '{index.TableName}' does not exist");

            m_indexes[index.Name] = index;
            SaveSchema();
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Drops an index.
    /// </summary>
    public bool DropIndex(string name)
    {
        m_lock.EnterWriteLock();
        try
        {
            if (!m_indexes.Remove(name))
                return false;

            SaveSchema();
            return true;
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    #endregion
}

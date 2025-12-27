using OutWit.Database.Definitions;
using OutWit.Database.Schema;
using OutWit.Database.Types;
using OutWit.Database.Utils;
using OutWit.Database.Values;

namespace OutWit.Database;

/// <summary>
/// DDL (Data Definition Language) operations for indexes in WitSqlEngine.
/// </summary>
public sealed partial class WitSqlEngine
{
    #region Create Index

    /// <summary>
    /// Create an index.
    /// </summary>
    /// <param name="index">The index definition.</param>
    public void CreateIndex(DefinitionIndex index)
    {
        // Store metadata in schema catalog
        m_schema.CreateIndex(index);
        
        // Create the physical secondary index in the database
        // This enables index lookups via m_database.GetIndex()
        if (m_database.SupportsIndexes)
        {
            m_database.CreateIndex(index.Name, index.IsUnique);
            
            // Build index from existing data in the table
            BuildIndexFromExistingData(index);
        }
    }

    /// <summary>
    /// Builds an index from existing data in the table.
    /// </summary>
    /// <param name="indexDef">The index definition.</param>
    private void BuildIndexFromExistingData(DefinitionIndex indexDef)
    {
        var table = m_schema.GetTable(indexDef.TableName);
        if (table == null)
            return;

        var secondaryIndex = m_database.GetIndex(indexDef.Name);
        if (secondaryIndex == null)
            return;

        // Scan all rows in the table
        var tablePrefix = SchemaCatalog.GetTableDataPrefix(indexDef.TableName);
        var endPrefix = SchemaCatalog.GetTableDataEndPrefix(indexDef.TableName);

        foreach (var (key, value) in m_database.Scan(tablePrefix, endPrefix))
        {
            // Parse row ID from key
            var rowId = SchemaCatalog.ParseRowId(key, indexDef.TableName);
            
            // Deserialize row
            var row = table.DeserializeRow(value);
            
            // Build index key
            var indexKey = BuildIndexKey(table, indexDef, row);
            if (indexKey == null)
                continue; // Skip rows with null values in indexed columns
            
            // Build primary key
            var primaryKey = BuildPrimaryKey(rowId);
            
            // Add to index
            try
            {
                secondaryIndex.Add(indexKey, primaryKey);
            }
            catch (InvalidOperationException)
            {
                // Unique index violation on existing data
                // Clean up the index and throw
                m_database.DropIndex(indexDef.Name);
                m_schema.DropIndex(indexDef.Name);
                throw new InvalidOperationException(
                    $"UNIQUE constraint failed: Cannot create unique index '{indexDef.Name}' " +
                    $"on table '{indexDef.TableName}' - duplicate values exist");
            }
        }
    }

    #endregion

    #region Get Index

    /// <summary>
    /// Get an index definition by name.
    /// </summary>
    /// <param name="indexName">The index name.</param>
    /// <returns>The index definition, or null if not found.</returns>
    public DefinitionIndex? GetIndex(string indexName)
    {
        return m_schema.GetIndex(indexName);
    }

    /// <summary>
    /// Get all indexes for a table.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>Collection of index definitions.</returns>
    public IEnumerable<DefinitionIndex> GetTableIndexes(string tableName)
    {
        return m_schema.GetTableIndexes(tableName);
    }

    /// <summary>
    /// Explicit interface implementation for IDatabase.GetIndex.
    /// </summary>
    DefinitionIndex? Interfaces.IDatabase.GetIndex(string indexName) => GetIndex(indexName);

    /// <summary>
    /// Explicit interface implementation for IDatabase.GetTableIndexes.
    /// </summary>
    IEnumerable<DefinitionIndex> Interfaces.IDatabase.GetTableIndexes(string tableName) => GetTableIndexes(tableName);

    #endregion

    #region Drop Index

    /// <summary>
    /// Drop an index.
    /// </summary>
    /// <param name="indexName">The index name to drop.</param>
    public void DropIndex(string indexName)
    {
        // Remove metadata from schema catalog
        m_schema.DropIndex(indexName);
        
        // Drop the physical secondary index from the database
        if (m_database.SupportsIndexes)
        {
            m_database.DropIndex(indexName);
        }
    }

    #endregion
}

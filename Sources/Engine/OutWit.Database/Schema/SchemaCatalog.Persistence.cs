using System.Text;
using OutWit.Common.MemoryPack;
using OutWit.Database.Definitions;

namespace OutWit.Database.Schema;

/// <summary>
/// Persistence part of SchemaCatalog.
/// </summary>
public sealed partial class SchemaCatalog
{
    #region Persistence

    private void LoadSchema()
    {
        // Load tables
        var tablesData = m_store.Get(TABLES_KEY_BYTES.AsSpan());
        if (tablesData != null)
        {
            var tableList = tablesData.FromMemoryPackBytes<List<DefinitionTable>>();
            if (tableList != null)
            {
                foreach (var table in tableList)
                {
                    m_tables[table.Name] = table;
                    LoadTableRowId(table.Name);
                    LoadTableRowCount(table.Name);
                }
            }
        }

        // Load indexes
        var indexesData = m_store.Get(INDEXES_KEY_BYTES.AsSpan());
        if (indexesData != null)
        {
            var indexList = indexesData.FromMemoryPackBytes<List<DefinitionIndex>>();
            if (indexList != null)
            {
                foreach (var index in indexList)
                {
                    m_indexes[index.Name] = index;
                }
            }
        }

        // Load views
        LoadViews();

        // Load triggers
        LoadTriggers();

        // Load sequences
        LoadSequences();

        // Load global row version counter
        LoadRowVersion();
    }

    private void SaveSchema()
    {
        // Save tables
        var tableList = m_tables.Values.ToList();
        m_store.Put(TABLES_KEY_BYTES.AsSpan(), tableList.ToMemoryPackBytes());

        // Save indexes
        var indexList = m_indexes.Values.ToList();
        m_store.Put(INDEXES_KEY_BYTES.AsSpan(), indexList.ToMemoryPackBytes());
    }

    #endregion
}

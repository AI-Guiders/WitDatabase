using System.Text;
using System.Text.Json;
using OutWit.Common.Json;
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
            var tableList = tablesData.FromJsonBytes<List<DefinitionTable>>();
            if (tableList != null)
            {
                foreach (var table in tableList)
                {
                    m_tables[table.Name] = table;
                    LoadTableRowId(table.Name);
                }
            }
        }

        // Load indexes
        var indexesData = m_store.Get(INDEXES_KEY_BYTES.AsSpan());
        if (indexesData != null)
        {
            var indexList = indexesData.FromJsonBytes<List<DefinitionIndex>>();
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
    }

    private void SaveSchema()
    {
        // Save tables
        var tableList = m_tables.Values.ToList();
        m_store.Put(TABLES_KEY_BYTES.AsSpan(), tableList.ToJsonBytes());

        // Save indexes
        var indexList = m_indexes.Values.ToList();
        m_store.Put(INDEXES_KEY_BYTES.AsSpan(), indexList.ToJsonBytes());
    }

    #endregion
}

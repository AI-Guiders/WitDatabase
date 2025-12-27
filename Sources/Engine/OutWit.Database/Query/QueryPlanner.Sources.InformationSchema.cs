using OutWit.Database.Definitions;
using OutWit.Database.Interfaces;
using OutWit.Database.Iterators;
using OutWit.Database.Parser.Schema.TableSources;
using OutWit.Database.Schema;

namespace OutWit.Database.Query;

/// <summary>
/// INFORMATION_SCHEMA iterator creation for QueryPlanner.
/// </summary>
public sealed partial class QueryPlanner
{
    #region INFORMATION_SCHEMA Iterator

    /// <summary>
    /// Creates an iterator for INFORMATION_SCHEMA virtual tables.
    /// </summary>
    private IResultIterator CreateInformationSchemaIterator(TableSourceSimple simple)
    {
        // Parse the view name from INFORMATION_SCHEMA.VIEW_NAME
        var tableName = simple.TableName;
        var viewName = tableName.Contains('.')
            ? tableName.Split('.', 2)[1]
            : tableName;

        // Get schema catalog from database to create InformationSchema helper
        // We need to get the schema catalog - it's stored in the context's database
        if (m_context.Database is not WitSqlEngine engine)
        {
            throw new InvalidOperationException("INFORMATION_SCHEMA is only available with WitSqlEngine");
        }

        var infoSchema = engine.GetInformationSchema();

        return viewName.ToUpperInvariant() switch
        {
            "TABLES" => new IteratorInformationSchema(
                infoSchema.GetTables(),
                InformationSchema.GetTablesColumns(),
                InformationSchema.GetTablesColumnTypes()),
                
            "COLUMNS" => new IteratorInformationSchema(
                infoSchema.GetColumns(),
                InformationSchema.GetColumnsColumns(),
                InformationSchema.GetColumnsColumnTypes()),
                
            "KEY_COLUMN_USAGE" => new IteratorInformationSchema(
                infoSchema.GetKeyColumnUsage(),
                InformationSchema.GetKeyColumnUsageColumns(),
                InformationSchema.GetKeyColumnUsageColumnTypes()),
                
            "TABLE_CONSTRAINTS" => new IteratorInformationSchema(
                infoSchema.GetTableConstraints(),
                InformationSchema.GetTableConstraintsColumns(),
                InformationSchema.GetTableConstraintsColumnTypes()),
                
            "REFERENTIAL_CONSTRAINTS" => new IteratorInformationSchema(
                infoSchema.GetReferentialConstraints(),
                InformationSchema.GetReferentialConstraintsColumns(),
                InformationSchema.GetReferentialConstraintsColumnTypes()),
                
            "INDEXES" => new IteratorInformationSchema(
                infoSchema.GetIndexes(),
                InformationSchema.GetIndexesColumns(),
                InformationSchema.GetIndexesColumnTypes()),
                
            "VIEWS" => new IteratorInformationSchema(
                infoSchema.GetViews(),
                InformationSchema.GetViewsColumns(),
                InformationSchema.GetViewsColumnTypes()),
                
            _ => throw new InvalidOperationException($"Unknown INFORMATION_SCHEMA view: {viewName}")
        };
    }

    #endregion
}

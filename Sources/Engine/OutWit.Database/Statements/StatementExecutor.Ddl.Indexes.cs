using OutWit.Database.Definitions;
using OutWit.Database.Parser.Serializers;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Statements;

/// <summary>
/// DDL execution for INDEX operations (CREATE, DROP).
/// </summary>
public sealed partial class StatementExecutor
{
    #region CREATE INDEX

    private WitSqlResult ExecuteCreateIndex(WitSqlStatementCreateIndex createIndex)
    {
        // Check if table exists
        var table = m_context.Database.GetTable(createIndex.TableName);
        if (table == null)
        {
            throw new InvalidOperationException($"Table '{createIndex.TableName}' not found");
        }

        // Check if index already exists when IF NOT EXISTS is specified
        if (createIndex.IfNotExists)
        {
            var existingIndex = m_context.Database.GetIndex(createIndex.IndexName);
            if (existingIndex != null)
            {
                return new WitSqlResult(); // Index already exists, do nothing
            }
        }

        // Build Columns list: for expression elements, use synthetic placeholder names
        // This ensures Columns.Count == ExpressionColumns.Count
        var columns = new List<string>();
        var expressionColumns = new List<string?>();
        
        for (int i = 0; i < createIndex.Elements.Count; i++)
        {
            var element = createIndex.Elements[i];
            if (element.ColumnName != null)
            {
                columns.Add(element.ColumnName);
                expressionColumns.Add(element.Expression != null 
                    ? WitSqlExpressionSerializer.Serialize(element.Expression) 
                    : null);
            }
            else if (element.Expression != null)
            {
                // Pure expression index element - use placeholder column name
                columns.Add($"$expr{i}");
                expressionColumns.Add(WitSqlExpressionSerializer.Serialize(element.Expression));
            }
        }

        var metadata = new DefinitionIndex
        {
            Name = createIndex.IndexName,
            TableName = createIndex.TableName,
            Columns = columns,
            IsUnique = createIndex.IsUnique,
            ColumnDescending = createIndex.Elements.Select(e => e.Descending).ToList(),
            WhereExpression = createIndex.WhereClause != null 
                ? WitSqlExpressionSerializer.Serialize(createIndex.WhereClause) 
                : null,
            IncludeColumns = createIndex.IncludeColumns,
            ExpressionColumns = expressionColumns
        };

        m_context.Database.CreateIndex(metadata);
        return new WitSqlResult();
    }

    #endregion

    #region DROP INDEX

    private WitSqlResult ExecuteDropIndex(WitSqlStatementDropIndex dropIndex)
    {
        // Check IF EXISTS
        if (dropIndex.IfExists)
        {
            var existingIndex = m_context.Database.GetIndex(dropIndex.IndexName);
            if (existingIndex == null)
            {
                return new WitSqlResult(); // Index doesn't exist, do nothing
            }
        }

        m_context.Database.DropIndex(dropIndex.IndexName);
        return new WitSqlResult();
    }

    #endregion
}

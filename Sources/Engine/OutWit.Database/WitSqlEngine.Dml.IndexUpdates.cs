using OutWit.Database.Context;
using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Parser;
using OutWit.Database.Types;
using OutWit.Database.Utils;
using OutWit.Database.Values;

namespace OutWit.Database;

/// <summary>
/// DML (Data Manipulation Language) operations for WitSqlEngine - index update logic.
/// </summary>
public sealed partial class WitSqlEngine
{
    #region Index Updates on Insert

    /// <summary>
    /// Updates all secondary indexes after a row insert.
    /// </summary>
    private void UpdateIndexesOnInsert(string tableName, DefinitionTable table, long rowId, WitSqlRow row)
    {
        var indexes = m_schema.GetTableIndexes(tableName);
        foreach (var indexDef in indexes)
        {
            var secondaryIndex = m_database.GetIndex(indexDef.Name);
            if (secondaryIndex == null)
                continue;

            // Check partial index WHERE condition
            if (!EvaluatePartialIndexCondition(indexDef, row))
                continue; // Row doesn't match partial index condition

            // Build the index key from the row values (supports expression indexes)
            var indexKey = BuildIndexKey(table, indexDef, row);
            if (indexKey == null)
                continue; // Skip if any key column is null and index doesn't support nulls

            // Build primary key (row ID in BigEndian format)
            var primaryKey = BuildPrimaryKey(rowId);

            // Add to index
            try
            {
                secondaryIndex.Add(indexKey, primaryKey);
            }
            catch (InvalidOperationException)
            {
                // Unique index violation - should have been caught by constraint validation
                // Re-throw with more context
                throw new InvalidOperationException(
                    $"UNIQUE constraint failed: Index '{indexDef.Name}' on table '{tableName}'");
            }
        }
    }

    #endregion

    #region Index Updates on Update

    /// <summary>
    /// Updates all secondary indexes after a row update.
    /// </summary>
    private void UpdateIndexesOnUpdate(string tableName, DefinitionTable table, long rowId, WitSqlRow? oldRow, WitSqlRow newRow)
    {
        var indexes = m_schema.GetTableIndexes(tableName);
        foreach (var indexDef in indexes)
        {
            var secondaryIndex = m_database.GetIndex(indexDef.Name);
            if (secondaryIndex == null)
                continue;

            var primaryKey = BuildPrimaryKey(rowId);

            // Check if old row was in index (partial index condition)
            bool oldRowInIndex = oldRow != null && EvaluatePartialIndexCondition(indexDef, oldRow.Value);
            bool newRowInIndex = EvaluatePartialIndexCondition(indexDef, newRow);

            // Build old and new index keys
            var oldIndexKey = oldRowInIndex ? BuildIndexKey(table, indexDef, oldRow!.Value) : null;
            var newIndexKey = newRowInIndex ? BuildIndexKey(table, indexDef, newRow) : null;

            // Check if the indexed columns actually changed
            bool keysEqual = oldIndexKey != null && newIndexKey != null && 
                             oldIndexKey.AsSpan().SequenceEqual(newIndexKey.AsSpan());

            if (keysEqual)
                continue; // No change to indexed columns

            // Remove old key if it existed
            if (oldIndexKey != null)
            {
                secondaryIndex.Remove(oldIndexKey, primaryKey);
            }

            // Add new key if not null
            if (newIndexKey != null)
            {
                try
                {
                    secondaryIndex.Add(newIndexKey, primaryKey);
                }
                catch (InvalidOperationException)
                {
                    // Unique index violation - rollback by re-adding old key
                    if (oldIndexKey != null)
                    {
                        secondaryIndex.Add(oldIndexKey, primaryKey);
                    }
                    throw new InvalidOperationException(
                        $"UNIQUE constraint failed: Index '{indexDef.Name}' on table '{tableName}'");
                }
            }
        }
    }

    #endregion

    #region Index Updates on Delete

    /// <summary>
    /// Updates all secondary indexes after a row delete.
    /// </summary>
    private void UpdateIndexesOnDelete(string tableName, DefinitionTable table, long rowId, WitSqlRow oldRow)
    {
        var indexes = m_schema.GetTableIndexes(tableName);
        foreach (var indexDef in indexes)
        {
            var secondaryIndex = m_database.GetIndex(indexDef.Name);
            if (secondaryIndex == null)
                continue;

            // Check partial index WHERE condition
            if (!EvaluatePartialIndexCondition(indexDef, oldRow))
                continue; // Row wasn't in this partial index

            var indexKey = BuildIndexKey(table, indexDef, oldRow);
            if (indexKey == null)
                continue;

            var primaryKey = BuildPrimaryKey(rowId);
            secondaryIndex.Remove(indexKey, primaryKey);
        }
    }

    #endregion

    #region Index Key Building

    /// <summary>
    /// Evaluates the WHERE condition of a partial/filtered index.
    /// </summary>
    /// <param name="indexDef">The index definition.</param>
    /// <param name="row">The row to evaluate against.</param>
    /// <returns>True if row should be included in index, false otherwise.</returns>
    private bool EvaluatePartialIndexCondition(DefinitionIndex indexDef, WitSqlRow row)
    {
        // If no WHERE clause, all rows are included
        if (string.IsNullOrEmpty(indexDef.WhereExpression))
            return true;

        try
        {
            // Parse and evaluate the WHERE expression
            var expression = WitSql.ParseExpression(indexDef.WhereExpression);
            var context = new ContextExecution { Database = this };
            var evaluator = new ExpressionEvaluator(context);
            var result = evaluator.Evaluate(expression, row);
            
            // Return true if expression evaluates to true (non-null, non-false)
            return result.IsTrue;
        }
        catch
        {
            // If evaluation fails, exclude from index for safety
            return false;
        }
    }

    /// <summary>
    /// Builds an index key from row values based on index definition.
    /// Supports expression indexes by evaluating expressions.
    /// </summary>
    /// <returns>The serialized index key, or null if any key column is null.</returns>
    private byte[]? BuildIndexKey(DefinitionTable table, DefinitionIndex indexDef, WitSqlRow row)
    {
        var keyValues = new WitSqlValue[indexDef.Columns.Count];
        var columnTypes = new WitDataType[indexDef.Columns.Count];

        for (int i = 0; i < indexDef.Columns.Count; i++)
        {
            var columnName = indexDef.Columns[i];
            WitSqlValue value;
            WitDataType columnType;
            
            // Check if this is an expression column
            var expressionSql = indexDef.GetColumnExpression(i);
            if (!string.IsNullOrEmpty(expressionSql))
            {
                // Evaluate the expression
                value = EvaluateIndexExpression(expressionSql, row);
                // For expression indexes, determine type from the result
                columnType = DetermineTypeFromValue(value);
            }
            else
            {
                // Skip _rowid - it's a system column not in the index
                if (columnName.Equals("_rowid", StringComparison.OrdinalIgnoreCase))
                    return null;
                    
                var column = table.GetColumn(columnName);
                if (column == null)
                    return null;

                columnType = column.Type;

                if (!row.TryGetValue(columnName, out value))
                    value = WitSqlValue.Null;
            }

            // Skip null values in index (standard SQL behavior for most DBs)
            if (value.IsNull)
                return null;

            keyValues[i] = value;
            columnTypes[i] = columnType;
        }

        return WitTypeConverter.SerializeIndexKey(keyValues, columnTypes);
    }

    /// <summary>
    /// Evaluates an expression for an expression index.
    /// </summary>
    private WitSqlValue EvaluateIndexExpression(string expressionSql, WitSqlRow row)
    {
        try
        {
            var expression = WitSql.ParseExpression(expressionSql);
            var context = new ContextExecution { Database = this };
            var evaluator = new ExpressionEvaluator(context);
            return evaluator.Evaluate(expression, row);
        }
        catch
        {
            // If evaluation fails, return null
            return WitSqlValue.Null;
        }
    }

    /// <summary>
    /// Determines the data type from a WitSqlValue for expression indexes.
    /// </summary>
    private static WitDataType DetermineTypeFromValue(WitSqlValue value)
    {
        return value.Type switch
        {
            WitSqlType.Integer => WitDataType.Int64,
            WitSqlType.Real => WitDataType.Float64,
            WitSqlType.Text => WitDataType.StringVariable,
            WitSqlType.Blob => WitDataType.BinaryVariable,
            WitSqlType.Boolean => WitDataType.Boolean,
            WitSqlType.Null => WitDataType.StringVariable, // Default for nulls
            _ => WitDataType.StringVariable
        };
    }

    /// <summary>
    /// Builds a primary key (row ID) in the format used by secondary indexes.
    /// </summary>
    private static byte[] BuildPrimaryKey(long rowId)
    {
        var key = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, rowId);
        return key;
    }

    #endregion
}

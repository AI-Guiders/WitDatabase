using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Iterators;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Statements;

/// <summary>
/// UPDATE statement execution.
/// </summary>
public sealed partial class StatementExecutor
{
    #region UPDATE

    private WitSqlResult ExecuteUpdate(WitSqlStatementUpdate update)
    {
        var table = m_context.Database.GetTable(update.TableName)
            ?? throw new InvalidOperationException($"Table '{update.TableName}' not found");

        // Validate that we're not trying to UPDATE computed columns or ROWVERSION directly
        foreach (var setClause in update.SetClauses)
        {
            var col = table.GetColumn(setClause.ColumnName);
            if (col != null && col.IsComputed)
            {
                throw new InvalidOperationException(
                    $"Cannot UPDATE computed column '{setClause.ColumnName}'");
            }
            if (col != null && col.Type == WitDataType.RowVersion)
            {
                throw new InvalidOperationException(
                    $"Cannot UPDATE ROWVERSION column '{setClause.ColumnName}'");
            }
        }

        // Create a full scan and filter
        var iterator = m_context.Database.CreateTableScan(update.TableName);

        if (update.WhereClause != null)
        {
            iterator = new IteratorFilter(iterator, update.WhereClause, m_context);
        }

        iterator.Open();
        var evaluator = new ExpressionEvaluator(m_context);

        // Get computed columns and ROWVERSION columns info
        var storedComputedColumns = table.Columns
            .Where(c => c.IsComputed && c.IsStored)
            .ToList();

        var rowVersionColumns = table.Columns
            .Where(c => c.Type == WitDataType.RowVersion)
            .ToList();

        // Collect rows to update (can't modify while iterating)
        var rowsToUpdate = new List<(long RowId, WitSqlRow OldRow, WitSqlRow NewRow)>();

        try
        {
            while (iterator.MoveNext())
            {
                var currentRow = iterator.Current;
                var oldRow = currentRow;
                var newValues = currentRow.Values.ToArray();
                var columnNames = currentRow.ColumnNames.ToArray();

                foreach (var setClause in update.SetClauses)
                {
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        if (columnNames[i].Equals(setClause.ColumnName, StringComparison.OrdinalIgnoreCase))
                        {
                            newValues[i] = evaluator.Evaluate(setClause.Value, currentRow);
                            break;
                        }
                    }
                }

                // Auto-increment ROWVERSION columns
                foreach (var rowVersionCol in rowVersionColumns)
                {
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        if (columnNames[i].Equals(rowVersionCol.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            newValues[i] = WitSqlValue.FromRowVersion(m_context.Database.GetNextRowVersion(table.Name));
                            break;
                        }
                    }
                }

                // Create intermediate row for computing computed columns
                var intermediateRow = new WitSqlRow(newValues, columnNames);

                // Recalculate STORED computed columns
                // Note: currentRow has _rowid at index 0, so we search by column name
                foreach (var computedCol in storedComputedColumns)
                {
                    // Find the column in the row (which includes _rowid as first column)
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        if (columnNames[i].Equals(computedCol.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(computedCol.ComputedExpression))
                            {
                                var expr = Parser.WitSql.ParseExpression(computedCol.ComputedExpression);
                                newValues[i] = evaluator.Evaluate(expr, intermediateRow);
                            }
                            break;
                        }
                    }
                }

                // Get row ID from _rowid column (added by TableScanIterator)
                var rowId = currentRow["_rowid"].AsInt64();
                rowsToUpdate.Add((rowId, oldRow, new WitSqlRow(newValues, columnNames)));
            }
        }
        finally
        {
            iterator.Dispose();
        }

        // Apply updates with trigger invocation
        int rowsAffected = 0;
        List<WitSqlRow>? returningRows = null;

        if (update.ReturningClause != null)
        {
            returningRows = [];
        }

        foreach (var (rowId, oldRow, originalNewRow) in rowsToUpdate)
        {
            var newRow = originalNewRow;

            // Fire BEFORE UPDATE triggers (can modify NEW row)
            WitSqlRow? newRowRef = newRow;
            if (!FireTriggers(update.TableName, TriggerEvent.Update, TriggerTime.Before, oldRow, ref newRowRef))
                continue; // Trigger cancelled

            newRow = newRowRef!.Value;

            // Check for INSTEAD OF triggers
            WitSqlRow? insteadOfRow = newRow;
            if (FireInsteadOfTrigger(update.TableName, TriggerEvent.Update, oldRow, ref insteadOfRow))
            {
                rowsAffected++;
                continue; // INSTEAD OF executed, skip normal update
            }

            // Validate NOT NULL constraints
            ValidateNotNullConstraints(table, newRow);

            ValidateConstraints(table, newRow, update.TableName, rowId);
            m_context.Database.UpdateRow(update.TableName, rowId, newRow);
            rowsAffected++;

            // Collect RETURNING row
            if (returningRows != null)
            {
                var returningRow = BuildReturningRow(newRow, update.ReturningClause!, table);
                returningRows.Add(returningRow);
            }

            // Fire AFTER UPDATE triggers
            WitSqlRow? afterRow = newRow;
            FireTriggers(update.TableName, TriggerEvent.Update, TriggerTime.After, oldRow, ref afterRow);
        }

        m_context.LastChangesCount = rowsAffected;

        // Return result with RETURNING rows if specified
        if (returningRows != null)
        {
            var schema = BuildReturningSchema(update.ReturningClause!, table);
            return new WitSqlResult(rowsAffected, returningRows, schema);
        }

        return new WitSqlResult(rowsAffected);
    }

    /// <summary>
    /// Validates NOT NULL constraints for a row.
    /// </summary>
    private static void ValidateNotNullConstraints(DefinitionTable table, WitSqlRow row)
    {
        foreach (var col in table.Columns)
        {
            if (!col.Nullable && row[col.Name].IsNull)
            {
                throw new InvalidOperationException($"NOT NULL constraint failed: {table.Name}.{col.Name}");
            }
        }
    }

    #endregion
}

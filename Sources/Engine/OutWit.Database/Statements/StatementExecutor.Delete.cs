using OutWit.Database.Definitions;
using OutWit.Database.Iterators;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Statements;

/// <summary>
/// DELETE statement execution.
/// </summary>
public sealed partial class StatementExecutor
{
    #region DELETE

    private WitSqlResult ExecuteDelete(WitSqlStatementDelete delete)
    {
        // Only get table definition if we have RETURNING clause (need schema for building return rows)
        DefinitionTable? table = null;
        if (delete.ReturningClause != null)
        {
            table = m_context.Database.GetTable(delete.TableName)
                ?? throw new InvalidOperationException($"Table '{delete.TableName}' not found");
        }

        var iterator = m_context.Database.CreateTableScan(delete.TableName);

        if (delete.WhereClause != null)
        {
            iterator = new IteratorFilter(iterator, delete.WhereClause, m_context);
        }

        iterator.Open();
        var rowsToDelete = new List<(long RowId, WitSqlRow OldRow)>();

        try
        {
            while (iterator.MoveNext())
            {
                var rowId = iterator.Current["_rowid"].AsInt64();
                rowsToDelete.Add((rowId, iterator.Current));
            }
        }
        finally
        {
            iterator.Dispose();
        }

        int rowsAffected = 0;
        List<WitSqlRow>? returningRows = null;

        if (delete.ReturningClause != null)
        {
            returningRows = [];
        }

        foreach (var (rowId, oldRow) in rowsToDelete)
        {
            // Fire BEFORE DELETE triggers
            WitSqlRow? newRow = null;
            if (!FireTriggers(delete.TableName, TriggerEvent.Delete, TriggerTime.Before, oldRow, ref newRow))
                continue; // Trigger cancelled

            // Check for INSTEAD OF triggers
            WitSqlRow? insteadOfRow = null;
            if (FireInsteadOfTrigger(delete.TableName, TriggerEvent.Delete, oldRow, ref insteadOfRow))
            {
                rowsAffected++;
                continue; // INSTEAD OF executed, skip normal delete
            }

            // Handle cascading actions for foreign keys referencing this table
            HandleCascadingActions(delete.TableName, oldRow, isDelete: true);

            // Collect RETURNING row before deletion
            if (returningRows != null && table != null)
            {
                var returningRow = BuildReturningRow(oldRow, delete.ReturningClause!, table);
                returningRows.Add(returningRow);
            }

            m_context.Database.DeleteRow(delete.TableName, rowId);
            rowsAffected++;

            // Fire AFTER DELETE triggers
            WitSqlRow? afterRow = null;
            FireTriggers(delete.TableName, TriggerEvent.Delete, TriggerTime.After, oldRow, ref afterRow);
        }

        m_context.LastChangesCount = rowsAffected;

        // Return result with RETURNING rows if specified
        if (returningRows != null && table != null)
        {
            var schema = BuildReturningSchema(delete.ReturningClause!, table);
            return new WitSqlResult(rowsAffected, returningRows, schema);
        }

        return new WitSqlResult(rowsAffected);
    }

    #endregion
}

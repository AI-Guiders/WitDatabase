using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Statements;

/// <summary>
/// INSERT statement execution including conflict resolution (UPSERT).
/// </summary>
public sealed partial class StatementExecutor
{
    #region INSERT

    private WitSqlResult ExecuteInsert(WitSqlStatementInsert insert)
    {
        var table = m_context.Database.GetTable(insert.TableName)
            ?? throw new InvalidOperationException($"Table '{insert.TableName}' not found");

        int rowsAffected = 0;
        long lastRowId = 0;
        List<WitSqlRow>? returningRows = null;

        if (insert.ReturningClause != null)
        {
            returningRows = [];
        }

        if (insert.Values != null)
        {
            foreach (var valueRow in insert.Values)
            {
                var (row, rowId) = BuildInsertRow(table, insert.ColumnNames, valueRow);

                // Handle conflict resolution
                var conflictResult = HandleConflictResolution(insert, table, ref row, ref rowId);
                if (conflictResult == ConflictResult.Skip)
                    continue;
                if (conflictResult == ConflictResult.Updated)
                {
                    rowsAffected++;
                    // Collect RETURNING row for upsert
                    if (returningRows != null)
                    {
                        var returningRow = BuildReturningRow(row, insert.ReturningClause!, table);
                        returningRows.Add(returningRow);
                    }
                    continue;
                }

                // Fire BEFORE INSERT triggers
                WitSqlRow? newRow = row;
                if (!FireTriggers(insert.TableName, TriggerEvent.Insert, TriggerTime.Before, null, ref newRow))
                    continue; // Trigger cancelled

                row = newRow!.Value;

                // Check for INSTEAD OF triggers
                WitSqlRow? insteadOfRow = row;
                if (FireInsteadOfTrigger(insert.TableName, TriggerEvent.Insert, null, ref insteadOfRow))
                {
                    rowsAffected++;
                    continue; // INSTEAD OF executed, skip normal insert
                }

                ValidateConstraints(table, row, insert.TableName);
                m_context.Database.InsertRow(insert.TableName, row);
                lastRowId = rowId;
                rowsAffected++;

                // Collect RETURNING row
                if (returningRows != null)
                {
                    var returningRow = BuildReturningRow(row, insert.ReturningClause!, table);
                    returningRows.Add(returningRow);
                }

                // Fire AFTER INSERT triggers
                WitSqlRow? afterRow = row;
                FireTriggers(insert.TableName, TriggerEvent.Insert, TriggerTime.After, null, ref afterRow);
            }
        }
        else if (insert.SelectSource != null)
        {
            rowsAffected = ExecuteInsertFromSelect(insert, table, returningRows);
        }

        // Update context for LAST_INSERT_ROWID() and CHANGES()
        m_context.LastInsertRowId = lastRowId;
        m_context.LastChangesCount = rowsAffected;

        // Return result with RETURNING rows if specified
        if (returningRows != null)
        {
            var schema = BuildReturningSchema(insert.ReturningClause!, table);
            return new WitSqlResult(rowsAffected, returningRows, schema);
        }

        return new WitSqlResult(rowsAffected);
    }

    private int ExecuteInsertFromSelect(WitSqlStatementInsert insert, DefinitionTable table, List<WitSqlRow>? returningRows)
    {
        var iterator = m_planner.Plan(insert.SelectSource!);
        iterator.Open();

        int rowsAffected = 0;
        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);

        try
        {
            while (iterator.MoveNext())
            {
                var values = new WitSqlValue[table.Columns.Count];
                var columnNames = table.Columns.Select(c => c.Name).ToArray();
                long rowId = 0;

                // Initialize with defaults and auto-increment (skip computed columns)
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var col = table.Columns[i];
                    if (col.IsComputed)
                    {
                        // Computed columns will be calculated after all regular values are set
                        values[i] = WitSqlValue.Null;
                    }
                    else if (col.IsAutoIncrement)
                    {
                        rowId = m_context.Database.GetNextAutoIncrement(table.Name);
                        values[i] = WitSqlValue.FromInt(rowId);
                    }
                    else if (col.DefaultValue != null)
                    {
                        var defaultExpr = Parser.WitSql.ParseExpression(col.DefaultValue);
                        values[i] = evaluator.Evaluate(defaultExpr, dummyRow);
                    }
                    else
                    {
                        values[i] = WitSqlValue.Null;
                    }
                }

                // Map SELECT columns to INSERT columns
                if (insert.ColumnNames != null && insert.ColumnNames.Count > 0)
                {
                    // Named columns: INSERT INTO table (col1, col2) SELECT ...
                    for (int i = 0; i < insert.ColumnNames.Count && i < iterator.Current.ColumnCount; i++)
                    {
                        var colIndex = table.GetOrdinal(insert.ColumnNames[i]);
                        if (colIndex >= 0)
                        {
                            var col = table.Columns[colIndex];
                            // Don't allow setting computed columns directly
                            if (col.IsComputed)
                            {
                                throw new InvalidOperationException(
                                    $"Cannot INSERT into computed column '{col.Name}'");
                            }
                            values[colIndex] = iterator.Current[i];
                        }
                    }
                }
                else
                {
                    // Positional: INSERT INTO table SELECT ...
                    // Skip computed columns for positional matching
                    int valueIndex = 0;
                    for (int i = 0; i < table.Columns.Count && valueIndex < iterator.Current.ColumnCount; i++)
                    {
                        var col = table.Columns[i];
                        if (!col.IsComputed)
                        {
                            values[i] = iterator.Current[valueIndex];
                            valueIndex++;
                        }
                    }
                }

                // Calculate STORED computed columns
                var intermediateRow = new WitSqlRow(values, columnNames);
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var col = table.Columns[i];
                    if (col.IsComputed && col.IsStored && !string.IsNullOrEmpty(col.ComputedExpression))
                    {
                        var expr = Parser.WitSql.ParseExpression(col.ComputedExpression);
                        values[i] = evaluator.Evaluate(expr, intermediateRow);
                    }
                }

                var row = new WitSqlRow(values, columnNames);

                // Fire BEFORE INSERT triggers
                WitSqlRow? newRow = row;
                if (!FireTriggers(insert.TableName, TriggerEvent.Insert, TriggerTime.Before, null, ref newRow))
                    continue;

                row = newRow!.Value;

                // Check for INSTEAD OF triggers
                WitSqlRow? insteadOfRow = row;
                if (FireInsteadOfTrigger(insert.TableName, TriggerEvent.Insert, null, ref insteadOfRow))
                {
                    rowsAffected++;
                    continue;
                }

                ValidateConstraints(table, row, insert.TableName);
                m_context.Database.InsertRow(insert.TableName, row);
                rowsAffected++;

                // Collect RETURNING row
                if (returningRows != null && insert.ReturningClause != null)
                {
                    var returningRow = BuildReturningRow(row, insert.ReturningClause, table);
                    returningRows.Add(returningRow);
                }

                // Fire AFTER INSERT triggers
                WitSqlRow? afterRow = row;
                FireTriggers(insert.TableName, TriggerEvent.Insert, TriggerTime.After, null, ref afterRow);
            }
        }
        finally
        {
            iterator.Dispose();
        }

        return rowsAffected;
    }

    private (WitSqlRow Row, long RowId) BuildInsertRow(
        DefinitionTable table,
        IReadOnlyList<string>? columnNames,
        IReadOnlyList<WitSqlExpression> valueExprs)
    {
        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);

        var values = new WitSqlValue[table.Columns.Count];
        var names = table.Columns.Select(c => c.Name).ToArray();
        long rowId = 0;

        // Initialize with defaults (skip computed columns for now)
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            if (col.IsComputed)
            {
                // Computed columns will be calculated after all regular values are set
                values[i] = WitSqlValue.Null;
            }
            else if (col.IsAutoIncrement)
            {
                rowId = m_context.Database.GetNextAutoIncrement(table.Name);
                values[i] = WitSqlValue.FromInt(rowId);
            }
            else if (col.DefaultValue != null)
            {
                // Parse and evaluate default expression
                var defaultExpr = Parser.WitSql.ParseExpression(col.DefaultValue);
                values[i] = evaluator.Evaluate(defaultExpr, dummyRow);
            }
            else
            {
                values[i] = WitSqlValue.Null;
            }
        }

        // Set specified values
        if (columnNames != null && columnNames.Count > 0)
        {
            // Named columns: INSERT INTO table (col1, col2) VALUES (val1, val2)
            for (int i = 0; i < columnNames.Count && i < valueExprs.Count; i++)
            {
                var colIndex = table.GetOrdinal(columnNames[i]);
                if (colIndex >= 0)
                {
                    var col = table.Columns[colIndex];
                    // Don't allow setting computed columns directly
                    if (col.IsComputed)
                    {
                        throw new InvalidOperationException(
                            $"Cannot INSERT into computed column '{col.Name}'");
                    }
                    values[colIndex] = evaluator.Evaluate(valueExprs[i], dummyRow);
                }
            }
        }
        else
        {
            // Positional: INSERT INTO table VALUES (val1, val2, ...)
            // Count non-computed columns for positional matching
            int valueIndex = 0;
            for (int i = 0; i < table.Columns.Count && valueIndex < valueExprs.Count; i++)
            {
                var col = table.Columns[i];
                if (!col.IsComputed)
                {
                    values[i] = evaluator.Evaluate(valueExprs[valueIndex], dummyRow);
                    valueIndex++;
                }
            }
        }

        // Now calculate STORED computed columns
        var intermediateRow = new WitSqlRow(values, names);
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            if (col.IsComputed && col.IsStored && !string.IsNullOrEmpty(col.ComputedExpression))
            {
                var expr = Parser.WitSql.ParseExpression(col.ComputedExpression);
                values[i] = evaluator.Evaluate(expr, intermediateRow);
            }
        }

        // Validate NOT NULL constraints
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            if (!col.Nullable && values[i].IsNull)
            {
                throw new InvalidOperationException($"NOT NULL constraint failed: {table.Name}.{col.Name}");
            }
        }

        return (new WitSqlRow(values, names), rowId);
    }

    #endregion

    #region Conflict Resolution (UPSERT)

    /// <summary>
    /// Result of conflict resolution check.
    /// </summary>
    private enum ConflictResult
    {
        /// <summary>No conflict, proceed with insert.</summary>
        NoConflict,

        /// <summary>Conflict detected, skip this row.</summary>
        Skip,

        /// <summary>Conflict detected, row was updated.</summary>
        Updated
    }

    /// <summary>
    /// Handles INSERT OR REPLACE, INSERT OR IGNORE, and ON CONFLICT clauses.
    /// </summary>
    private ConflictResult HandleConflictResolution(WitSqlStatementInsert insert, DefinitionTable table, ref WitSqlRow row, ref long rowId)
    {
        // Find conflicting row based on primary key or unique constraints
        var (existingRowId, existingRow) = FindConflictingRow(table, row, insert.OnConflict?.ConflictColumns);

        if (existingRowId == null)
            return ConflictResult.NoConflict;

        // Handle INSERT OR REPLACE
        if (insert.ConflictResolution == ConflictResolutionType.Replace)
        {
            // Delete existing row and proceed with insert
            m_context.Database.DeleteRow(table.Name, existingRowId.Value);
            return ConflictResult.NoConflict;
        }

        // Handle INSERT OR IGNORE
        if (insert.ConflictResolution == ConflictResolutionType.Ignore)
        {
            return ConflictResult.Skip;
        }

        // Handle ON CONFLICT clause
        if (insert.OnConflict != null)
        {
            if (insert.OnConflict.ActionType == ConflictActionType.Nothing)
            {
                return ConflictResult.Skip;
            }

            if (insert.OnConflict.ActionType == ConflictActionType.Update)
            {
                // Set EXCLUDED pseudo-table before evaluating WHERE clause
                // (it may reference EXCLUDED.column)
                m_context.ExcludedRow = row;

                try
                {
                    // Check WHERE clause if present
                    if (insert.OnConflict.WhereClause != null)
                    {
                        var evaluator = new ExpressionEvaluator(m_context);
                        var whereResult = evaluator.Evaluate(insert.OnConflict.WhereClause, existingRow!.Value);
                        if (!whereResult.IsTrue)
                        {
                            return ConflictResult.Skip;
                        }
                    }

                    // Execute UPDATE with SET clauses
                    row = ExecuteUpsertUpdate(table, existingRowId.Value, existingRow!.Value, row, insert.OnConflict.UpdateClauses!);
                    rowId = existingRowId.Value;
                    return ConflictResult.Updated;
                }
                finally
                {
                    m_context.ExcludedRow = null;
                }
            }
        }

        return ConflictResult.NoConflict;
    }

    /// <summary>
    /// Finds a row that conflicts with the given row based on primary key or unique constraints.
    /// Also checks unique indexes for conflict detection.
    /// </summary>
    private (long? RowId, WitSqlRow? Row) FindConflictingRow(DefinitionTable table, WitSqlRow newRow, IReadOnlyList<string>? conflictColumns)
    {
        // If specific conflict columns are specified (ON CONFLICT (columns)), use only those
        if (conflictColumns != null && conflictColumns.Count > 0)
        {
            return FindConflictingRowByColumns(table, newRow, conflictColumns);
        }

        // Check primary key columns first
        if (table.PrimaryKey != null && table.PrimaryKey.Count > 0)
        {
            // Only check if all PK columns have non-null values in newRow
            bool allPkColumnsHaveValues = table.PrimaryKey.All(pkCol =>
            {
                if (!newRow.TryGetValue(pkCol, out var value))
                    return false;
                return !value.IsNull;
            });

            if (allPkColumnsHaveValues)
            {
                var result = FindConflictingRowByColumns(table, newRow, table.PrimaryKey);
                if (result.RowId != null)
                    return result;
            }
        }

        // Check unique columns on the table
        var uniqueColumns = table.Columns.Where(c => c.IsUnique).Select(c => c.Name).ToList();
        foreach (var uniqueCol in uniqueColumns)
        {
            if (newRow.TryGetValue(uniqueCol, out var value) && !value.IsNull)
            {
                var result = FindConflictingRowByColumns(table, newRow, [uniqueCol]);
                if (result.RowId != null)
                    return result;
            }
        }

        // Check unique indexes
        var uniqueIndexes = m_context.Database.GetTableIndexes(table.Name).Where(i => i.IsUnique && !i.IsPrimaryKey);
        foreach (var index in uniqueIndexes)
        {
            // Only check if all index columns have non-null values in newRow
            bool allIndexColumnsHaveValues = index.Columns.All(col =>
            {
                if (!newRow.TryGetValue(col, out var value))
                    return false;
                return !value.IsNull;
            });

            if (allIndexColumnsHaveValues)
            {
                var result = FindConflictingRowByColumns(table, newRow, index.Columns);
                if (result.RowId != null)
                    return result;
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a row that conflicts by checking specific columns.
    /// </summary>
    private (long? RowId, WitSqlRow? Row) FindConflictingRowByColumns(DefinitionTable table, WitSqlRow newRow, IReadOnlyList<string> columnsToCheck)
    {
        if (columnsToCheck.Count == 0)
            return (null, null);

        var iterator = m_context.Database.CreateTableScan(table.Name);
        iterator.Open();

        try
        {
            while (iterator.MoveNext())
            {
                var existingRow = iterator.Current;
                bool allMatch = true;

                foreach (var colName in columnsToCheck)
                {
                    if (!newRow.TryGetValue(colName, out var newValue))
                    {
                        allMatch = false;
                        break;
                    }

                    if (!existingRow.TryGetValue(colName, out var existingValue))
                    {
                        allMatch = false;
                        break;
                    }

                    // Skip NULL values - NULL does not equal NULL for conflict detection
                    if (newValue.IsNull || existingValue.IsNull)
                    {
                        allMatch = false;
                        break;
                    }

                    if (!newValue.Equals(existingValue))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    var rowId = existingRow["_rowid"].AsInt64();
                    return (rowId, existingRow);
                }
            }
        }
        finally
        {
            iterator.Dispose();
        }

        return (null, null);
    }

    /// <summary>
    /// Executes the UPDATE part of an upsert operation.
    /// Note: ExcludedRow must be set in context before calling this method.
    /// </summary>
    private WitSqlRow ExecuteUpsertUpdate(DefinitionTable table, long rowId, WitSqlRow existingRow, WitSqlRow newRow, IReadOnlyList<ClauseSet> updateClauses)
    {
        var evaluator = new ExpressionEvaluator(m_context);
        var newValues = existingRow.Values.ToArray();
        var columnNames = existingRow.ColumnNames.ToArray();

        // EXCLUDED pseudo-table is already set in context by HandleConflictResolution
        foreach (var setClause in updateClauses)
        {
            for (int i = 0; i < columnNames.Length; i++)
            {
                if (columnNames[i].Equals(setClause.ColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    newValues[i] = evaluator.Evaluate(setClause.Value, existingRow);
                    break;
                }
            }
        }

        // Recalculate STORED computed columns
        var storedComputedColumns = table.Columns.Where(c => c.IsComputed && c.IsStored).ToList();
        var intermediateRow = new WitSqlRow(newValues, columnNames);

        foreach (var computedCol in storedComputedColumns)
        {
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

        var updatedRow = new WitSqlRow(newValues, columnNames);

        // Validate constraints
        ValidateNotNullConstraints(table, updatedRow);
        ValidateConstraints(table, updatedRow, table.Name, rowId);

        // Perform update
        m_context.Database.UpdateRow(table.Name, rowId, updatedRow);

        return updatedRow;
    }

    #endregion
}

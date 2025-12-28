using OutWit.Database.Iterators;
using OutWit.Database.Model;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Sql;
using OutWit.Database.Values;

namespace OutWit.Database.Query;

/// <summary>
/// CTE (Common Table Expression) handling for QueryPlanner.
/// </summary>
public sealed partial class QueryPlanner
{
    #region CTE Registration

    /// <summary>
    /// Registers CTE definitions from the WITH clause into the execution context.
    /// </summary>
    private void RegisterCteDefinitions(Parser.Statements.WitSqlStatementSelect select)
    {
        if (select.CteDefinitions == null || select.CteDefinitions.Count == 0)
            return;

        foreach (var cte in select.CteDefinitions)
        {
            if (m_context.CteDefinitions.ContainsKey(cte.Name))
            {
                throw new InvalidOperationException($"Duplicate CTE name: '{cte.Name}'");
            }

            // For recursive CTEs, we need to mark them specially
            if (select.IsRecursive)
            {
                // Store with a marker that this is a recursive CTE
                m_context.CteDefinitions[cte.Name] = cte;
                m_context.State[$"CTE_RECURSIVE_{cte.Name}"] = true;
            }
            else
            {
                m_context.CteDefinitions[cte.Name] = cte;
            }
        }
    }

    /// <summary>
    /// Tries to resolve a table name as a CTE reference.
    /// </summary>
    /// <param name="tableName">The table name to resolve.</param>
    /// <returns>The CTE definition if found, null otherwise.</returns>
    private ClauseCteDefinition? TryGetCteDefinition(string tableName)
    {
        return m_context.CteDefinitions.TryGetValue(tableName, out var cteDef) 
            ? cteDef 
            : null;
    }

    /// <summary>
    /// Checks if a CTE is marked as recursive.
    /// </summary>
    private bool IsRecursiveCte(string cteName)
    {
        return m_context.State.TryGetValue($"CTE_RECURSIVE_{cteName}", out var value) 
               && value is true;
    }

    #endregion

    #region CTE Working Table Management

    /// <summary>
    /// Gets the in-memory working table for recursive CTE execution.
    /// </summary>
    private List<WitSqlRow>? GetRecursiveWorkingTable(string cteName)
    {
        return m_context.State.TryGetValue($"CTE_WORKING_{cteName}", out var value)
            ? value as List<WitSqlRow>
            : null;
    }

    /// <summary>
    /// Gets the schema for the recursive CTE working table.
    /// </summary>
    private IReadOnlyList<WitSqlColumnInfo>? GetRecursiveWorkingTableSchema(string cteName)
    {
        return m_context.State.TryGetValue($"CTE_SCHEMA_{cteName}", out var value)
            ? value as IReadOnlyList<WitSqlColumnInfo>
            : null;
    }

    /// <summary>
    /// Sets the in-memory working table for recursive CTE execution.
    /// </summary>
    private void SetRecursiveWorkingTable(string cteName, List<WitSqlRow> rows, IReadOnlyList<WitSqlColumnInfo>? schema = null)
    {
        m_context.State[$"CTE_WORKING_{cteName}"] = rows;
        if (schema != null)
        {
            m_context.State[$"CTE_SCHEMA_{cteName}"] = schema;
        }
    }

    #endregion

    #region CTE Caching

    /// <summary>
    /// Tries to get cached CTE results.
    /// </summary>
    /// <param name="cteName">The CTE name.</param>
    /// <returns>The cached entry if found, null otherwise.</returns>
    private CteCacheEntry? TryGetCachedCte(string cteName)
    {
        return m_context.CteCache.TryGetValue(cteName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Caches CTE results for reuse.
    /// </summary>
    /// <param name="cteName">The CTE name.</param>
    /// <param name="rows">The rows to cache.</param>
    /// <param name="schema">The schema of the cached rows.</param>
    private void CacheCteResults(string cteName, IReadOnlyList<WitSqlRow> rows, IReadOnlyList<WitSqlColumnInfo> schema)
    {
        m_context.CteCache[cteName] = new CteCacheEntry
        {
            Rows = rows,
            Schema = schema
        };
    }

    #endregion

    #region CTE Iterator Creation

    private Interfaces.IResultIterator CreateCteIterator(ClauseCteDefinition cteDef, string alias)
    {
        // Check if we already have cached results for this CTE
        var cached = TryGetCachedCte(cteDef.Name);
        if (cached != null)
        {
            // Use cached results
            var cachedIterator = new IteratorInMemory(cached.Rows, cached.Schema);
            return WrapWithAlias(cachedIterator, alias);
        }
        
        // Execute CTE and cache results
        var cteIterator = Plan(cteDef.Query);
        
        // If CTE has explicit column names, rename the columns
        if (cteDef.ColumnNames != null && cteDef.ColumnNames.Count > 0)
        {
            cteIterator = new IteratorColumnRename(cteIterator, cteDef.ColumnNames);
        }
        
        // Materialize the CTE results into memory and cache them
        var rows = new List<WitSqlRow>();
        cteIterator.Open();
        try
        {
            // Get schema after opening (schema might be built during Open)
            var schema = cteIterator.Schema;
            
            while (cteIterator.MoveNext())
            {
                rows.Add(cteIterator.Current);
            }
            
            // Cache the results
            CacheCteResults(cteDef.Name, rows, schema);
            
            // Return iterator over cached results
            var cachedIterator = new IteratorInMemory(rows, schema);
            return WrapWithAlias(cachedIterator, alias);
        }
        finally
        {
            cteIterator.Dispose();
        }
    }

    private Interfaces.IResultIterator CreateRecursiveCteIterator(ClauseCteDefinition cteDef, string alias)
    {
        var cteQuery = cteDef.Query;
        
        // Recursive CTE must have UNION ALL
        if (cteQuery.SetOperations == null || cteQuery.SetOperations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Recursive CTE '{cteDef.Name}' must contain UNION ALL");
        }

        var setOp = cteQuery.SetOperations[0];
        if (!setOp.IsAll)
        {
            throw new InvalidOperationException(
                $"Recursive CTE '{cteDef.Name}' requires UNION ALL, not UNION");
        }

        // Step 1: Execute anchor member (the base SELECT before UNION ALL)
        var anchorQuery = new Parser.Statements.WitSqlStatementSelect
        {
            IsDistinct = cteQuery.IsDistinct,
            IsRecursive = false,
            CteDefinitions = null,
            SelectList = cteQuery.SelectList,
            FromClause = cteQuery.FromClause,
            WhereClause = cteQuery.WhereClause,
            GroupByClause = cteQuery.GroupByClause,
            HavingClause = cteQuery.HavingClause
        };

        var allRows = new List<WitSqlRow>();
        var workingTable = new List<WitSqlRow>();
        IReadOnlyList<WitSqlColumnInfo>? schema = null;
        
        // Execute anchor
        var anchorIterator = Plan(anchorQuery);
        anchorIterator.Open();
        try
        {
            schema = anchorIterator.Schema;
            
            while (anchorIterator.MoveNext())
            {
                var row = RenameRowColumns(anchorIterator.Current, cteDef.ColumnNames);
                workingTable.Add(row);
                allRows.Add(row);
            }
        }
        finally
        {
            anchorIterator.Dispose();
        }

        // Update schema with renamed columns if needed
        if (cteDef.ColumnNames != null && cteDef.ColumnNames.Count > 0 && schema != null)
        {
            schema = RenameSchemaColumns(schema, cteDef.ColumnNames);
        }

        // Step 2: Iteratively execute recursive member
        var recursiveQuery = setOp.RightQuery;
        int recursionDepth = 0;

        while (workingTable.Count > 0 && recursionDepth < MAX_RECURSION_DEPTH)
        {
            recursionDepth++;
            
            // Set working table for recursive reference with schema
            SetRecursiveWorkingTable(cteDef.Name, workingTable, schema);
            
            var newRows = new List<WitSqlRow>();
            
            // Execute recursive member
            var recursiveIterator = Plan(recursiveQuery);
            recursiveIterator.Open();
            try
            {
                while (recursiveIterator.MoveNext())
                {
                    var row = RenameRowColumns(recursiveIterator.Current, cteDef.ColumnNames);
                    newRows.Add(row);
                    allRows.Add(row);
                }
            }
            finally
            {
                recursiveIterator.Dispose();
            }

            // Swap working table for next iteration
            workingTable = newRows;
        }

        // Clear working table state
        m_context.State.Remove($"CTE_WORKING_{cteDef.Name}");
        m_context.State.Remove($"CTE_SCHEMA_{cteDef.Name}");

        if (recursionDepth >= MAX_RECURSION_DEPTH)
        {
            throw new InvalidOperationException(
                $"Recursive CTE '{cteDef.Name}' exceeded maximum recursion depth of {MAX_RECURSION_DEPTH}");
        }

        // Cache the results for potential reuse
        if (schema != null)
        {
            CacheCteResults(cteDef.Name, allRows, schema);
        }

        // Return in-memory iterator over all collected rows
        var resultIterator = new IteratorInMemory(allRows, schema ?? []);
        return WrapWithAlias(resultIterator, alias);
    }

    #endregion

    #region CTE Helper Methods

    private static WitSqlRow RenameRowColumns(WitSqlRow row, IReadOnlyList<string>? newNames)
    {
        if (newNames == null || newNames.Count == 0)
            return row;

        var names = new string[row.ColumnCount];
        for (int i = 0; i < row.ColumnCount; i++)
        {
            names[i] = i < newNames.Count ? newNames[i] : row.ColumnNames[i];
        }

        return new WitSqlRow(row.Values.ToArray(), names);
    }

    private static IReadOnlyList<WitSqlColumnInfo> RenameSchemaColumns(
        IReadOnlyList<WitSqlColumnInfo> schema, 
        IReadOnlyList<string> newNames)
    {
        var result = new List<WitSqlColumnInfo>(schema.Count);
        
        for (int i = 0; i < schema.Count; i++)
        {
            var newName = i < newNames.Count ? newNames[i] : schema[i].Name;
            result.Add(new WitSqlColumnInfo
            {
                Name = newName,
                Type = schema[i].Type,
                IsNullable = schema[i].IsNullable,
                TableName = schema[i].TableName
            });
        }
        
        return result;
    }

    #endregion
}

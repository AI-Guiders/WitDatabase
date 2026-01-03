using System.Data.Common;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using OutWit.Database.AdoNet;

namespace OutWit.Database.EntityFramework.Design.Internal;

/// <summary>
/// Reverse engineers a WitDatabase database into a model.
/// Used by 'dotnet ef dbcontext scaffold' command.
/// </summary>
public class WitDatabaseModelFactory : IDatabaseModelFactory
{
    #region IDatabaseModelFactory

    /// <summary>
    /// Creates a model from the database.
    /// </summary>
    public DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
    {
        using var connection = new WitDbConnection(connectionString);
        return Create(connection, options);
    }

    /// <summary>
    /// Creates a model from an existing connection.
    /// </summary>
    public DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var model = new DatabaseModel();
        
        var needsClose = connection.State != System.Data.ConnectionState.Open;
        if (needsClose)
        {
            connection.Open();
        }

        try
        {
            model.DatabaseName = GetDatabaseName(connection);
            
            // Get tables
            foreach (var table in GetTables(connection, options))
            {
                model.Tables.Add(table);
            }
        }
        finally
        {
            if (needsClose)
            {
                connection.Close();
            }
        }

        return model;
    }

    #endregion

    #region Helper Methods

    private static string? GetDatabaseName(DbConnection connection)
    {
        // Extract database name from connection string
        var connectionString = connection.ConnectionString ?? string.Empty;
        
        // Look for "Data Source=" in connection string
        var index = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var start = index + "Data Source=".Length;
            var end = connectionString.IndexOf(';', start);
            var dataSource = end >= 0 
                ? connectionString.Substring(start, end - start) 
                : connectionString[start..];
            
            return Path.GetFileNameWithoutExtension(dataSource);
        }
        
        return null;
    }

    private static IEnumerable<DatabaseTable> GetTables(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var tables = new List<DatabaseTable>();
        var tablesToFilter = options.Tables.ToList();
        
        // Query system tables for table metadata
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT name FROM sqlite_master 
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            
            // Skip if table is not in the options filter
            if (tablesToFilter.Count > 0 && !tablesToFilter.Contains(tableName))
            {
                continue;
            }
            
            var table = new DatabaseTable { Name = tableName };
            
            // Get columns for this table
            GetColumns(connection, table);
            
            // Get primary key
            GetPrimaryKey(connection, table);
            
            // Get indexes
            GetIndexes(connection, table);
            
            // Get foreign keys
            GetForeignKeys(connection, table);
            
            tables.Add(table);
        }
        
        return tables;
    }

    private static void GetColumns(DbConnection connection, DatabaseTable table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table.Name}\")";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var column = new DatabaseColumn
            {
                Table = table,
                Name = reader.GetString(1),
                StoreType = reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                DefaultValueSql = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
            
            // Check if this is a primary key column
            if (reader.GetInt32(5) > 0)
            {
                column.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
            }
            
            table.Columns.Add(column);
        }
    }

    private static void GetPrimaryKey(DbConnection connection, DatabaseTable table)
    {
        var pkColumns = table.Columns
            .Where(c => c.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd)
            .ToList();
        
        if (pkColumns.Count > 0)
        {
            var pk = new DatabasePrimaryKey
            {
                Table = table,
                Name = $"PK_{table.Name}"
            };
            
            foreach (var column in pkColumns)
            {
                pk.Columns.Add(column);
            }
            
            table.PrimaryKey = pk;
        }
    }

    private static void GetIndexes(DbConnection connection, DatabaseTable table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA index_list(\"{table.Name}\")";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var indexName = reader.GetString(1);
            var isUnique = reader.GetInt32(2) == 1;
            
            var index = new DatabaseIndex
            {
                Table = table,
                Name = indexName,
                IsUnique = isUnique
            };
            
            // Get index columns
            using var colCmd = connection.CreateCommand();
            colCmd.CommandText = $"PRAGMA index_info(\"{indexName}\")";
            
            using var colReader = colCmd.ExecuteReader();
            while (colReader.Read())
            {
                var columnName = colReader.GetString(2);
                var column = table.Columns.FirstOrDefault(c => c.Name == columnName);
                if (column != null)
                {
                    index.Columns.Add(column);
                }
            }
            
            table.Indexes.Add(index);
        }
    }

    private static void GetForeignKeys(DbConnection connection, DatabaseTable table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list(\"{table.Name}\")";
        
        var fkGroups = new Dictionary<int, DatabaseForeignKey>();
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var principalTableName = reader.GetString(2);
            var columnName = reader.GetString(3);
            var principalColumnName = reader.GetString(4);
            var onUpdate = reader.GetString(5);
            var onDelete = reader.GetString(6);
            
            if (!fkGroups.TryGetValue(id, out var fk))
            {
                fk = new DatabaseForeignKey
                {
                    Table = table,
                    Name = $"FK_{table.Name}_{principalTableName}_{id}",
                    PrincipalTable = new DatabaseTable { Name = principalTableName },
                    OnDelete = ParseReferentialAction(onDelete)
                };
                fkGroups[id] = fk;
            }
            
            var column = table.Columns.FirstOrDefault(c => c.Name == columnName);
            if (column != null)
            {
                fk.Columns.Add(column);
            }
            
            fk.PrincipalColumns.Add(new DatabaseColumn { Name = principalColumnName });
        }
        
        foreach (var fk in fkGroups.Values)
        {
            table.ForeignKeys.Add(fk);
        }
    }

    private static ReferentialAction ParseReferentialAction(string action)
    {
        return action.ToUpperInvariant() switch
        {
            "CASCADE" => ReferentialAction.Cascade,
            "RESTRICT" => ReferentialAction.Restrict,
            "SET NULL" => ReferentialAction.SetNull,
            "SET DEFAULT" => ReferentialAction.SetDefault,
            _ => ReferentialAction.NoAction
        };
    }

    #endregion
}

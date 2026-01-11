using Microsoft.Extensions.Logging;
using OutWit.Database.Studio.Models;
using System.Diagnostics;

namespace OutWit.Database.Studio.Services;


/// <summary>
/// Query execution methods for DatabaseService.
/// </summary>
public sealed partial class DatabaseService
{
    #region Query Execution

    public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)
    {
        EnsureConnected();

        var result = new QueryResult();
        var sw = Stopwatch.StartNew();

        try
        {
            result = await ExecuteQueryInternalAsync(sql, ct);
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;

            m_logger.LogInformation(
                "Query executed successfully in {Time}ms, {Rows} rows",
                result.ExecutionTimeMs, result.RowsAffected);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = FormatErrorMessage(ex);
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            m_logger.LogError(ex, "Query execution failed");
        }
        finally
        {
            sw.Stop();
        }

        return result;
    }

    private async Task<QueryResult> ExecuteQueryInternalAsync(string sql, CancellationToken ct)
    {
        using var command = m_connection!.CreateCommand();
        command.CommandText = sql;

        using var reader = await command.ExecuteReaderAsync(ct);

        var dataTable = CreateDataTableFromReader(reader);
        await PopulateDataTableAsync(dataTable, reader, ct);

        return new QueryResult
        {
            Data = dataTable,
            RowsAffected = dataTable.Rows.Count
        };
    }

    private static System.Data.DataTable CreateDataTableFromReader(System.Data.Common.DbDataReader reader)
    {
        var dataTable = new System.Data.DataTable("QueryResult");

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var columnType = reader.GetFieldType(i);
            dataTable.Columns.Add(columnName, columnType);
        }

        return dataTable;
    }

    private static async Task PopulateDataTableAsync(
        System.Data.DataTable dataTable,
        System.Data.Common.DbDataReader reader,
        CancellationToken ct)
    {
        while (await reader.ReadAsync(ct))
        {
            var row = dataTable.NewRow();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
            }
            dataTable.Rows.Add(row);
        }
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
    {
        EnsureConnected();

        using var command = m_connection!.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, CancellationToken ct = default)
    {
        EnsureConnected();

        using var command = m_connection!.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync(ct);
    }

    #endregion
}

using System.Text;
using OutWit.Database.Engine;

namespace OutWit.Database.Native;

internal static class WitDbSqlInterop
{
    public static WitDbStatusCode SqlExec(
        UIntPtr dbHandle,
        string sql,
        string? paramsJson,
        out long lastInsertRowId,
        out int rowsAffected)
    {
        lastInsertRowId = 0;
        rowsAffected = 0;

        if (!WitDbHandleTable.TryGetDatabase(dbHandle, out var entry) || entry is null)
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidHandle, "Unknown database handle");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidArgument, "sql is required");
        }

        try
        {
            var engine = GetOrCreateEngine(entry);
            var (boundSql, parameters) = WitDbSqlHelpers.BindSql(sql, paramsJson);
            using var result = engine.Execute(boundSql, parameters);
            rowsAffected = result.RowsAffected;
            lastInsertRowId = engine.LastInsertRowId;
            WitDbLastError.Set(null);
            return WitDbStatusCode.Ok;
        }
        catch (Exception ex)
        {
            return MapSqlException(ex);
        }
    }

    public static WitDbStatusCode SqlQuery(
        UIntPtr dbHandle,
        string sql,
        string? paramsJson,
        out byte[]? resultJson)
    {
        resultJson = null;

        if (!WitDbHandleTable.TryGetDatabase(dbHandle, out var entry) || entry is null)
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidHandle, "Unknown database handle");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidArgument, "sql is required");
        }

        try
        {
            var engine = GetOrCreateEngine(entry);
            var (boundSql, parameters) = WitDbSqlHelpers.BindSql(sql, paramsJson);
            using var result = engine.Execute(boundSql, parameters);
            if (!result.HasRows)
            {
                resultJson = Encoding.UTF8.GetBytes("""{"columns":[],"rows":[]}""");
                WitDbLastError.Set(null);
                return WitDbStatusCode.Ok;
            }

            var columns = result.Columns.Select(c => c.Name).ToList();
            var rows = result.ReadAll();
            resultJson = Encoding.UTF8.GetBytes(WitDbSqlHelpers.BuildQueryJson(rows, columns));
            WitDbLastError.Set(null);
            return WitDbStatusCode.Ok;
        }
        catch (Exception ex)
        {
            return MapSqlException(ex);
        }
    }

    public static WitDbStatusCode SqlCommit(UIntPtr dbHandle)
    {
        if (!WitDbHandleTable.TryGetDatabase(dbHandle, out var entry) || entry is null)
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidHandle, "Unknown database handle");
        }

        try
        {
            var engine = GetOrCreateEngine(entry);
            engine.Commit();
            entry.Database.Flush();
            WitDbLastError.Set(null);
            return WitDbStatusCode.Ok;
        }
        catch (Exception ex)
        {
            return MapSqlException(ex);
        }
    }

    public static WitDbStatusCode SqlRollback(UIntPtr dbHandle)
    {
        if (!WitDbHandleTable.TryGetDatabase(dbHandle, out var entry) || entry is null)
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidHandle, "Unknown database handle");
        }

        try
        {
            var engine = GetOrCreateEngine(entry);
            engine.Rollback();
            WitDbLastError.Set(null);
            return WitDbStatusCode.Ok;
        }
        catch (Exception ex)
        {
            return MapSqlException(ex);
        }
    }

    private static WitSqlEngine GetOrCreateEngine(DbEntry entry)
    {
        if (entry.SqlEngine is null)
        {
            entry.SqlEngine = new WitSqlEngine(entry.Database, ownsStore: false);
        }

        return entry.SqlEngine;
    }

    private static WitDbStatusCode MapSqlException(Exception ex)
    {
        WitDbLastError.Set(ex.Message);
        return ex switch
        {
            ArgumentException => WitDbStatusCode.InvalidArgument,
            InvalidOperationException => WitDbStatusCode.SqlError,
            _ => WitDbInterop.MapException(ex),
        };
    }
}

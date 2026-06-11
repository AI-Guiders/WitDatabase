namespace OutWit.Database.Native;

/// <summary>
/// Managed implementation behind UCO exports (Marshal / heavy work stays out of UCO thunks).
/// </summary>
internal static class WitDbExportsCore
{
    public static unsafe WitDbStatusCode Open(
        byte* path,
        byte* password,
        bool createIfMissing,
        UIntPtr* outDb)
    {
        var pathStr = WitDbUtf8.PtrToString(path);
        if (string.IsNullOrWhiteSpace(pathStr))
        {
            return WitDbInterop.Fail(WitDbStatusCode.InvalidArgument, "path is required");
        }

        string? passwordStr = password == null ? null : WitDbUtf8.PtrToString(password);
        var status = WitDbInterop.Open(pathStr, passwordStr, createIfMissing, out var handle);
        *outDb = handle;
        return status;
    }

    public static unsafe WitDbStatusCode SqlExec(
        UIntPtr db,
        byte* sql,
        byte* paramsJson,
        long* outLastRowid,
        int* outRowsAffected)
    {
        var sqlStr = WitDbUtf8.PtrToString(sql);
        string? paramsStr = paramsJson == null ? null : WitDbUtf8.PtrToString(paramsJson);
        var status = WitDbSqlInterop.SqlExec(db, sqlStr, paramsStr, out var lastRowid, out var rowsAffected);
        *outLastRowid = lastRowid;
        *outRowsAffected = rowsAffected;
        return status;
    }

    public static unsafe WitDbStatusCode SqlQuery(
        UIntPtr db,
        byte* sql,
        byte* paramsJson,
        byte** outResultJson,
        uint* outResultLen)
    {
        var sqlStr = WitDbUtf8.PtrToString(sql);
        string? paramsStr = paramsJson == null ? null : WitDbUtf8.PtrToString(paramsJson);
        var status = WitDbSqlInterop.SqlQuery(db, sqlStr, paramsStr, out var json);
        if (status != WitDbStatusCode.Ok || json is null)
        {
            *outResultJson = null;
            *outResultLen = 0;
            return status;
        }

        *outResultJson = WitDbInterop.AllocCopy(json);
        *outResultLen = (uint)json.Length;
        return WitDbStatusCode.Ok;
    }
}

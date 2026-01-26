using OutWit.Database.Definitions;
using OutWit.Database.Parser.Schema;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Types;

namespace OutWit.Database.Statements;

/// <summary>
/// Type mapping utilities for DDL execution.
/// </summary>
public sealed partial class StatementExecutor
{
    #region SQL Type to WitDataType Mapping

    /// <summary>
    /// Maps a SQL data type to the internal WitDataType.
    /// </summary>
    private static WitDataType MapDataType(WitSqlDataType dataType)
    {
        return dataType.TypeName.ToUpperInvariant() switch
        {
            // Integer types
            "TINYINT" or "INT8" => WitDataType.Int8,
            "UTINYINT" or "UINT8" or "BYTE" => WitDataType.UInt8,
            "SMALLINT" or "INT16" or "SHORT" => WitDataType.Int16,
            "USMALLINT" or "UINT16" or "USHORT" => WitDataType.UInt16,
            "INT" or "INT32" or "INTEGER" => WitDataType.Int32,
            "UINT" or "UINT32" => WitDataType.UInt32,
            "BIGINT" or "INT64" or "LONG" => WitDataType.Int64,
            "UBIGINT" or "UINT64" or "ULONG" => WitDataType.UInt64,
            
            // Floating point types
            "FLOAT16" or "HALF" => WitDataType.Float16,
            "FLOAT" or "FLOAT32" or "REAL" => WitDataType.Float32,
            "DOUBLE" or "FLOAT64" => WitDataType.Float64,
            "DECIMAL" or "NUMERIC" or "MONEY" => WitDataType.Decimal,
            
            // Boolean
            "BOOLEAN" or "BOOL" or "BIT" => WitDataType.Boolean,
            
            // Date/Time types
            "DATE" or "DATEONLY" => WitDataType.DateOnly,
            "TIME" or "TIMEONLY" => WitDataType.TimeOnly,
            "DATETIME" or "TIMESTAMP" or "DATETIME2" => WitDataType.DateTime,
            "DATETIMEOFFSET" => WitDataType.DateTimeOffset,
            "TIMESPAN" or "DURATION" or "INTERVAL" => WitDataType.TimeSpan,
            
            // GUID
            "GUID" or "UUID" or "UNIQUEIDENTIFIER" => WitDataType.Guid,
            
            // String types
            "CHAR" or "NCHAR" => WitDataType.StringFixed,
            "VARCHAR" or "NVARCHAR" or "TEXT" or "NTEXT" or "STRING" => WitDataType.StringVariable,
            
            // Binary types
            "BINARY" => WitDataType.BinaryFixed,
            "VARBINARY" or "BLOB" => WitDataType.BinaryVariable,
            
            // Special types
            "ROWVERSION" => WitDataType.RowVersion,
            "JSON" or "JSONB" => WitDataType.Json,
            
            _ => WitDataType.StringVariable
        };
    }

    #endregion

    #region Reference Action Mapping

    /// <summary>
    /// Maps a SQL reference action type to the internal ReferenceAction.
    /// </summary>
    private static ReferenceAction MapReferenceAction(ReferenceActionType action)
    {
        return action switch
        {
            ReferenceActionType.NoAction => ReferenceAction.NoAction,
            ReferenceActionType.Restrict => ReferenceAction.Restrict,
            ReferenceActionType.Cascade => ReferenceAction.Cascade,
            ReferenceActionType.SetNull => ReferenceAction.SetNull,
            ReferenceActionType.SetDefault => ReferenceAction.SetDefault,
            _ => ReferenceAction.NoAction
        };
    }

    #endregion
}

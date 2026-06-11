using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Native;

internal static class WitDbSqlHelpers
{
    public static (string Sql, Dictionary<string, object?> Parameters) BindSql(
        string sql,
        string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            return (sql, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
        }

        using var document = JsonDocument.Parse(paramsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("params_json must be a JSON array");
        }

        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            parameters[$"@p{index}"] = JsonToObject(item);
            index++;
        }

        if (index == 0)
        {
            return (sql, parameters);
        }

        return (ReplaceQuestionMarks(sql, index), parameters);
    }

    private static string ReplaceQuestionMarks(string sql, int paramCount)
    {
        var seen = 0;
        var inSingle = false;
        var inDouble = false;
        var result = new System.Text.StringBuilder(sql.Length + paramCount * 2);

        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (ch == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                result.Append(ch);
                continue;
            }

            if (ch == '"' && !inSingle)
            {
                inDouble = !inDouble;
                result.Append(ch);
                continue;
            }

            if (ch == '?' && !inSingle && !inDouble)
            {
                if (seen >= paramCount)
                {
                    throw new ArgumentException("more SQL placeholders than parameters");
                }

                result.Append("@p").Append(seen);
                seen++;
                continue;
            }

            result.Append(ch);
        }

        if (seen != paramCount)
        {
            throw new ArgumentException("fewer SQL placeholders than parameters");
        }

        return result.ToString();
    }

    public static object? JsonToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt64(out var i) => i,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        _ => throw new ArgumentException($"unsupported JSON parameter type: {element.ValueKind}"),
    };

    public static JsonNode? ValueToJson(WitSqlValue value)
    {
        if (value.IsNull)
        {
            return null;
        }

        return value.Type switch
        {
            WitSqlType.Boolean => value.AsBool(),
            WitSqlType.Integer => value.AsInt64(),
            WitSqlType.Real => value.AsDouble(),
            WitSqlType.Text => value.AsString(),
            WitSqlType.Decimal => value.AsDecimal().ToString(CultureInfo.InvariantCulture),
            WitSqlType.DateTime => value.AsDateTime().ToString("O", CultureInfo.InvariantCulture),
            WitSqlType.DateOnly => value.AsDateOnly().ToString("O", CultureInfo.InvariantCulture),
            WitSqlType.TimeOnly => value.AsTimeOnly().ToString("O", CultureInfo.InvariantCulture),
            WitSqlType.TimeSpan => value.AsTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            WitSqlType.DateTimeOffset => value.AsDateTimeOffset().ToString("O", CultureInfo.InvariantCulture),
            WitSqlType.Guid => value.AsGuid().ToString(),
            WitSqlType.Blob => Convert.ToBase64String(value.AsBlob() ?? []),
            WitSqlType.Json => JsonNode.Parse(value.AsString() ?? "null"),
            WitSqlType.RowVersion => value.AsUInt64(),
            _ => value.AsString(),
        };
    }

    public static string BuildQueryJson(IReadOnlyList<WitSqlRow> rows, IReadOnlyList<string> columns)
    {
        var root = new JsonObject
        {
            ["columns"] = new JsonArray(columns.Select(c => JsonValue.Create(c)).ToArray()),
            ["rows"] = RowsToJson(rows, columns),
        };
        return root.ToJsonString();
    }

    private static JsonArray RowsToJson(IReadOnlyList<WitSqlRow> rows, IReadOnlyList<string> columns)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            var rowNode = new JsonArray();
            for (var i = 0; i < columns.Count; i++)
            {
                rowNode.Add(ValueToJson(row[i]));
            }

            array.Add(rowNode);
        }

        return array;
    }
}

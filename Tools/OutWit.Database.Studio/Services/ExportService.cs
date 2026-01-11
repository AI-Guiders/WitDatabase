using System.Data;
using System.Text;

namespace OutWit.Database.Studio.Services;

/// <summary>
/// Service for exporting query results to various formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports DataTable to CSV format.
    /// </summary>
    Task ExportToCsvAsync(DataTable data, string filePath);

    /// <summary>
    /// Exports DataTable to JSON format.
    /// </summary>
    Task ExportToJsonAsync(DataTable data, string filePath);

    /// <summary>
    /// Exports DataTable to SQL INSERT statements.
    /// </summary>
    Task ExportToSqlAsync(DataTable data, string tableName, string filePath);

    /// <summary>
    /// Converts DataTable rows to CSV string.
    /// </summary>
    string ToCsv(DataTable data, bool includeHeaders = true);

    /// <summary>
    /// Converts DataTable rows to INSERT statements.
    /// </summary>
    string ToInsertStatements(DataTable data, string tableName);

    /// <summary>
    /// Converts selected rows to CSV string.
    /// </summary>
    string RowsToCsv(IEnumerable<DataRowView> rows, DataTable schema, bool includeHeaders = true);

    /// <summary>
    /// Converts selected rows to INSERT statements.
    /// </summary>
    string RowsToInsertStatements(IEnumerable<DataRowView> rows, DataTable schema, string tableName);
}

/// <summary>
/// Implementation of export service.
/// </summary>
public class ExportService : IExportService
{
    #region Constants

    private const string NULL_SQL = "NULL";
    private const int MAX_BLOB_DISPLAY_LENGTH = 32;

    #endregion

    #region IExportService

    public async Task ExportToCsvAsync(DataTable data, string filePath)
    {
        var csv = ToCsv(data);
        await File.WriteAllTextAsync(filePath, csv, Encoding.UTF8);
    }

    public async Task ExportToJsonAsync(DataTable data, string filePath)
    {
        var json = ToJson(data);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public async Task ExportToSqlAsync(DataTable data, string tableName, string filePath)
    {
        var sql = ToInsertStatements(data, tableName);
        await File.WriteAllTextAsync(filePath, sql, Encoding.UTF8);
    }

    public string ToCsv(DataTable data, bool includeHeaders = true)
    {
        var sb = new StringBuilder();

        if (includeHeaders)
        {
            var headers = data.Columns.Cast<DataColumn>()
                .Select(c => EscapeCsvField(c.ColumnName));
            sb.AppendLine(string.Join(",", headers));
        }

        foreach (DataRow row in data.Rows)
        {
            var values = row.ItemArray.Select(v => EscapeCsvField(FormatValue(v)));
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    public string ToInsertStatements(DataTable data, string tableName)
    {
        if (data.Rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => QuoteIdentifier(c.ColumnName)));

        foreach (DataRow row in data.Rows)
        {
            var values = new List<string>();
            for (var i = 0; i < data.Columns.Count; i++)
            {
                values.Add(FormatSqlValue(row[i], data.Columns[i].DataType));
            }
            sb.AppendLine($"INSERT INTO {QuoteIdentifier(tableName)} ({columns}) VALUES ({string.Join(", ", values)});");
        }

        return sb.ToString();
    }

    public string RowsToCsv(IEnumerable<DataRowView> rows, DataTable schema, bool includeHeaders = true)
    {
        var sb = new StringBuilder();
        var rowList = rows.ToList();

        if (rowList.Count == 0)
            return string.Empty;

        if (includeHeaders)
        {
            var headers = schema.Columns.Cast<DataColumn>()
                .Select(c => EscapeCsvField(c.ColumnName));
            sb.AppendLine(string.Join(",", headers));
        }

        foreach (var rowView in rowList)
        {
            var values = rowView.Row.ItemArray.Select(v => EscapeCsvField(FormatValue(v)));
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    public string RowsToInsertStatements(IEnumerable<DataRowView> rows, DataTable schema, string tableName)
    {
        var sb = new StringBuilder();
        var rowList = rows.ToList();

        if (rowList.Count == 0)
            return string.Empty;

        var columns = string.Join(", ", schema.Columns.Cast<DataColumn>().Select(c => QuoteIdentifier(c.ColumnName)));

        foreach (var rowView in rowList)
        {
            var values = new List<string>();
            for (var i = 0; i < schema.Columns.Count; i++)
            {
                values.Add(FormatSqlValue(rowView.Row[i], schema.Columns[i].DataType));
            }
            sb.AppendLine($"INSERT INTO {QuoteIdentifier(tableName)} ({columns}) VALUES ({string.Join(", ", values)});");
        }

        return sb.ToString();
    }

    #endregion

    #region Functions

    private static string ToJson(DataTable data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        var rowIndex = 0;
        foreach (DataRow row in data.Rows)
        {
            sb.Append("  {");

            var columnIndex = 0;
            foreach (DataColumn column in data.Columns)
            {
                var value = row[column];
                var jsonValue = FormatJsonValue(value);

                sb.Append($"\"{column.ColumnName}\": {jsonValue}");

                if (columnIndex < data.Columns.Count - 1)
                    sb.Append(", ");

                columnIndex++;
            }

            sb.Append('}');

            if (rowIndex < data.Rows.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();

            rowIndex++;
        }

        sb.AppendLine("]");
        return sb.ToString();
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string FormatValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return string.Empty;

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            TimeOnly t => t.ToString("HH:mm:ss"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
            byte[] bytes => bytes.Length <= MAX_BLOB_DISPLAY_LENGTH
                ? $"0x{BitConverter.ToString(bytes).Replace("-", "")}"
                : $"0x{BitConverter.ToString(bytes, 0, MAX_BLOB_DISPLAY_LENGTH).Replace("-", "")}...",
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatSqlValue(object? value, Type dataType)
    {
        if (value == null || value == DBNull.Value)
            return NULL_SQL;

        // Numbers don't need quotes
        if (IsNumericType(dataType))
            return value.ToString() ?? NULL_SQL;

        // Boolean
        if (dataType == typeof(bool))
            return (bool)value ? "TRUE" : "FALSE";

        // Binary
        if (value is byte[] bytes)
            return $"X'{BitConverter.ToString(bytes).Replace("-", "")}'";

        // GUID
        if (value is Guid guid)
            return $"'{guid}'";

        // Escape single quotes and wrap in quotes
        var str = FormatValue(value);
        return $"'{str.Replace("'", "''")}'";
    }

    private static string FormatJsonValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "null";

        return value switch
        {
            string str => $"\"{EscapeJsonString(str)}\"",
            bool b => b ? "true" : "false",
            DateTime dt => $"\"{dt:yyyy-MM-ddTHH:mm:ss}\"",
            DateOnly d => $"\"{d:yyyy-MM-dd}\"",
            TimeOnly t => $"\"{t:HH:mm:ss}\"",
            DateTimeOffset dto => $"\"{dto:yyyy-MM-ddTHH:mm:sszzz}\"",
            TimeSpan ts => $"\"{ts:hh\\:mm\\:ss}\"",
            byte[] bytes => $"\"{Convert.ToBase64String(bytes)}\"",
            Guid guid => $"\"{guid}\"",
            int or long or short or byte or sbyte or uint or ulong or ushort => value.ToString() ?? "null",
            float or double or decimal => value.ToString() ?? "null",
            _ => $"\"{EscapeJsonString(value.ToString() ?? string.Empty)}\""
        };
    }

    private static string EscapeJsonString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]")}]";
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    #endregion
}

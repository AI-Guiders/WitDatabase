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

        // Headers
        if (includeHeaders)
        {
            var headers = data.Columns.Cast<DataColumn>()
                .Select(c => EscapeCsvField(c.ColumnName));
            sb.AppendLine(string.Join(",", headers));
        }

        // Rows
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
            var values = row.ItemArray.Select(v => FormatSqlValue(v));
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

        // Headers
        if (includeHeaders)
        {
            var headers = schema.Columns.Cast<DataColumn>()
                .Select(c => EscapeCsvField(c.ColumnName));
            sb.AppendLine(string.Join(",", headers));
        }

        // Rows
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
            var values = rowView.Row.ItemArray.Select(v => FormatSqlValue(v));
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

        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
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

        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss");

        if (value is byte[] bytes)
            return $"0x{BitConverter.ToString(bytes).Replace("-", "")}";

        return value.ToString() ?? string.Empty;
    }

    private static string FormatSqlValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "NULL";

        if (value is string str)
            return $"'{str.Replace("'", "''")}'";

        if (value is DateTime dt)
            return $"'{dt:yyyy-MM-dd HH:mm:ss}'";

        if (value is bool b)
            return b ? "1" : "0";

        if (value is byte[] bytes)
            return $"X'{BitConverter.ToString(bytes).Replace("-", "")}'";

        if (value is Guid guid)
            return $"'{guid}'";

        return value.ToString() ?? "NULL";
    }

    private static string FormatJsonValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "null";

        if (value is string str)
            return $"\"{EscapeJsonString(str)}\"";

        if (value is bool b)
            return b ? "true" : "false";

        if (value is DateTime dt)
            return $"\"{dt:yyyy-MM-ddTHH:mm:ss}\"";

        if (value is byte[] bytes)
            return $"\"{Convert.ToBase64String(bytes)}\"";

        if (value is Guid guid)
            return $"\"{guid}\"";

        if (value is int or long or short or byte or float or double or decimal)
            return value.ToString() ?? "null";

        return $"\"{EscapeJsonString(value.ToString() ?? string.Empty)}\"";
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

    #endregion
}

using System.Globalization;
using Avalonia.Data.Converters;

namespace OutWit.Database.Studio.Converters;

/// <summary>
/// Converter that formats SQL values for display in DataGrid.
/// Displays "(NULL)" for null values and formats binary data as hex.
/// </summary>
public class SqlValueConverter : IValueConverter
{
    #region Constants

    public const string NULL_DISPLAY_TEXT = "(NULL)";
    private const int MAX_BLOB_DISPLAY_LENGTH = 16;

    #endregion

    #region IValueConverter

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value == DBNull.Value)
            return NULL_DISPLAY_TEXT;

        return value switch
        {
            byte[] bytes => FormatBlob(bytes),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            TimeOnly t => t.ToString("HH:mm:ss"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
            bool b => b ? "true" : "false",
            _ => value
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && str == NULL_DISPLAY_TEXT)
            return null;

        return value;
    }

    #endregion

    #region Functions

    private static string FormatBlob(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "(empty)";

        if (bytes.Length <= MAX_BLOB_DISPLAY_LENGTH)
            return $"0x{BitConverter.ToString(bytes).Replace("-", "")}";

        var preview = BitConverter.ToString(bytes, 0, MAX_BLOB_DISPLAY_LENGTH).Replace("-", "");
        return $"0x{preview}... ({bytes.Length} bytes)";
    }

    #endregion
}
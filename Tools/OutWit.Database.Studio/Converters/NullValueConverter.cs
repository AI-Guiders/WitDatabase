using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OutWit.Database.Studio.Converters;

/// <summary>
/// Converter that displays "(NULL)" for null or empty values.
/// </summary>
public class NullValueConverter : IValueConverter
{
    #region Constants

    public const string NULL_DISPLAY_TEXT = "(NULL)";

    #endregion

    #region IValueConverter

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return NULL_DISPLAY_TEXT;

        var text = value.ToString();
        
        return string.IsNullOrEmpty(text) ? NULL_DISPLAY_TEXT : text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If value is the NULL display text, return null
        if (value is string text && text == NULL_DISPLAY_TEXT)
            return null;

        // Otherwise return the value as-is
        return value;
    }

    #endregion
}

/// <summary>
/// Converter that returns true if value is null or empty (for styling).
/// </summary>
public class IsNullOrEmptyConverter : IValueConverter
{
    #region IValueConverter

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return true;

        var text = value.ToString();
        return string.IsNullOrEmpty(text);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is typically used for one-way styling bindings
        // Return the value as-is for safety
        return value;
    }

    #endregion
}

/// <summary>
/// Converter that returns a specific brush for null values.
/// </summary>
public class NullValueBrushConverter : IValueConverter
{
    #region Fields

    private static readonly IBrush s_nullBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
    private static readonly IBrush s_normalBrush = Brushes.Black;

    #endregion

    #region IValueConverter

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return s_nullBrush;

        var text = value.ToString();
        return string.IsNullOrEmpty(text) ? s_nullBrush : s_normalBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is for brush bindings, ConvertBack is not meaningful
        return value;
    }

    #endregion
}

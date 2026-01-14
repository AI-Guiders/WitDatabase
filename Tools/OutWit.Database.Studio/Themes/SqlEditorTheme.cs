using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace OutWit.Database.Studio.Themes;

/// <summary>
/// Provides SQL Editor theme colors from application resources.
/// </summary>
public static class SqlEditorTheme
{
    #region Constants

    private const string BG_KEY = "SqlEditorBg";
    private const string FG_KEY = "SqlEditorFg";
    private const string LN_KEY = "SqlEditorLineNumbers";

    private static readonly Color DEFAULT_BACKGROUND_LIGHT = Color.Parse("#FFFFFF");
    private static readonly Color DEFAULT_FOREGROUND_LIGHT = Color.Parse("#1E1E1E");
    private static readonly Color DEFAULT_LINE_NUMBERS_LIGHT = Color.Parse("#6E6E6E");

    private static readonly Color DEFAULT_BACKGROUND_DARK = Color.Parse("#1E1E1E");
    private static readonly Color DEFAULT_FOREGROUND_DARK = Color.Parse("#D4D4D4");
    private static readonly Color DEFAULT_LINE_NUMBERS_DARK = Color.Parse("#858585");

    #endregion

    #region Functions

    /// <summary>
    /// Gets a color from application resources.
    /// </summary>
    private static Color GetColor(string key, Color lightDefault, Color darkDefault)
    {
        var isDark = IsDarkTheme();
        var defaultColor = isDark ? darkDefault : lightDefault;

        if (Application.Current?.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) == true)
        {
            if (resource is Color color)
                return color;
        }

        return defaultColor;
    }

    /// <summary>
    /// Determines if the current theme is dark.
    /// </summary>
    public static bool IsDarkTheme()
    {
        if (Application.Current == null)
            return false;

        return Application.Current.ActualThemeVariant == ThemeVariant.Dark;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the editor background color for the current theme.
    /// </summary>
    public static Color BackgroundColor =>
        GetColor(BG_KEY, DEFAULT_BACKGROUND_LIGHT, DEFAULT_BACKGROUND_DARK);

    /// <summary>
    /// Gets the editor foreground color for the current theme.
    /// </summary>
    public static Color ForegroundColor =>
        GetColor(FG_KEY, DEFAULT_FOREGROUND_LIGHT, DEFAULT_FOREGROUND_DARK);

    /// <summary>
    /// Gets the line numbers color for the current theme.
    /// </summary>
    public static Color LineNumbersColor =>
        GetColor(LN_KEY, DEFAULT_LINE_NUMBERS_LIGHT, DEFAULT_LINE_NUMBERS_DARK);

    /// <summary>
    /// Gets the editor background brush.
    /// </summary>
    public static SolidColorBrush BackgroundBrush => new(BackgroundColor);

    /// <summary>
    /// Gets the editor foreground brush.
    /// </summary>
    public static SolidColorBrush ForegroundBrush => new(ForegroundColor);

    /// <summary>
    /// Gets the line numbers brush.
    /// </summary>
    public static SolidColorBrush LineNumbersBrush => new(LineNumbersColor);

    #endregion
}

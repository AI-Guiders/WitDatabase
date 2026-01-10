using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using OutWit.Database.Studio.Syntax;

namespace OutWit.Database.Studio.Controls;

/// <summary>
/// SQL Editor control with syntax highlighting based on AvaloniaEdit.
/// </summary>
public class SqlEditor : TextEditor
{
    #region Dependency Properties

    public static readonly StyledProperty<string> SqlTextProperty =
        AvaloniaProperty.Register<SqlEditor, string>(nameof(SqlText), string.Empty);

    #endregion

    #region Fields

    private bool m_isUpdatingText;

    #endregion

    #region Constructors

    public SqlEditor()
    {
        // Set up appearance
        FontFamily = new FontFamily("Consolas, Courier New, monospace");
        FontSize = 13;
        ShowLineNumbers = true;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
        LineNumbersForeground = new SolidColorBrush(Color.Parse("#858585"));
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        WordWrap = false;
        Padding = new Thickness(4);

        // Apply WitSQL syntax highlighting
        SyntaxHighlighting = WitSqlHighlighting.Definition;

        // Configure editor options
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;
        Options.ShowSpaces = false;
        Options.ShowTabs = false;
        Options.HighlightCurrentLine = true;

        // Subscribe to text changes
        TextChanged += OnEditorTextChanged;

        // Handle property changes
        SqlTextProperty.Changed.AddClassHandler<SqlEditor>((x, e) => x.OnSqlTextPropertyChanged(e));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the SQL text content (bindable).
    /// </summary>
    public string SqlText
    {
        get => GetValue(SqlTextProperty);
        set => SetValue(SqlTextProperty, value);
    }

    #endregion

    #region Event Handlers

    private void OnSqlTextPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (m_isUpdatingText)
            return;

        var newText = e.NewValue as string ?? string.Empty;
        if (Text != newText)
        {
            m_isUpdatingText = true;
            Text = newText;
            m_isUpdatingText = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (m_isUpdatingText)
            return;

        m_isUpdatingText = true;
        SqlText = Text;
        m_isUpdatingText = false;
    }

    #endregion
}

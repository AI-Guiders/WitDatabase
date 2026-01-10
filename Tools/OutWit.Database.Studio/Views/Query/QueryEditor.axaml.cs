using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace OutWit.Database.Studio.Views.Query;

/// <summary>
/// Query editor view for SQL execution.
/// </summary>
public partial class QueryEditor : UserControl
{
    #region Constructors

    public QueryEditor()
    {
        InitializeComponent();
    }

    #endregion

    #region Functions

    /// <summary>
    /// Gets the selected text from the SQL TextBox.
    /// </summary>
    public string? GetSelectedText()
    {
        var textBox = this.FindLogicalDescendantOfType<TextBox>();
        if (textBox == null)
            return null;

        if (textBox.SelectionStart >= 0 && textBox.SelectionEnd > textBox.SelectionStart)
        {
            var text = textBox.Text;
            if (!string.IsNullOrEmpty(text))
            {
                var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
                var length = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);
                
                if (start >= 0 && start + length <= text.Length)
                {
                    return text.Substring(start, length);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Sets focus to the SQL TextBox.
    /// </summary>
    public void FocusEditor()
    {
        var textBox = this.FindLogicalDescendantOfType<TextBox>();
        if (textBox != null)
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
        }
    }

    #endregion
}

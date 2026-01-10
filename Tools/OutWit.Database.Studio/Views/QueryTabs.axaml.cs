using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OutWit.Database.Studio.Models;
using OutWit.Database.Studio.ViewModels;
using OutWit.Database.Studio.Views.Query;

namespace OutWit.Database.Studio.Views;

/// <summary>
/// View for managing multiple query editor tabs.
/// </summary>
public partial class QueryTabs : UserControl
{
    #region Constructors

    public QueryTabs()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusEditor(), 
            Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnTabHeaderClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is QueryTab tab)
        {
            if (DataContext is QueryTabsViewModel viewModel)
            {
                viewModel.SelectedTab = tab;
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusEditor(), 
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    #endregion

    #region Functions

    /// <summary>
    /// Gets the selected SQL text from the current QueryEditor.
    /// </summary>
    public string? GetSelectedSqlText()
    {
        var editor = this.FindLogicalDescendantOfType<QueryEditor>();
        return editor?.GetSelectedText();
    }

    /// <summary>
    /// Sets focus to the SQL editor.
    /// </summary>
    public void FocusEditor()
    {
        var editor = this.FindLogicalDescendantOfType<QueryEditor>();
        editor?.FocusEditor();
    }

    #endregion
}

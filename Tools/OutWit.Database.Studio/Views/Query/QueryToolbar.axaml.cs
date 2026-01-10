using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views.Query;

/// <summary>
/// Toolbar for query execution commands.
/// </summary>
public partial class QueryToolbar : UserControl
{
    #region Constructors

    public QueryToolbar()
    {
        InitializeComponent();
    }

    #endregion

    #region Event Handlers

    private void OnExecuteSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not QueryTabsViewModel viewModel)
            return;

        // Find QueryEditor in parent tree and get selected text
        var queryTabs = this.FindLogicalAncestorOfType<QueryTabs>();
        var selectedText = queryTabs?.GetSelectedSqlText();
        
        if (viewModel.ExecuteSelectionCommand.CanExecute(selectedText))
        {
            viewModel.ExecuteSelectionCommand.Execute(selectedText);
        }
    }

    #endregion
}
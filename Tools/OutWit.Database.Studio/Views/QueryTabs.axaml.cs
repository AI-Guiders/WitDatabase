using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OutWit.Database.Studio.Models;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views;

/// <summary>
/// View for managing multiple query editor tabs.
/// </summary>
public partial class QueryTabs : UserControl
{
    public QueryTabs()
    {
        InitializeComponent();
        
        // Set focus when control is loaded
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Handles control loaded event to set initial focus.
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Try to focus the SQL editor when the control is first loaded
        Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusSqlEditor(), 
            Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Handles tab header click to select the tab.
    /// </summary>
    private void OnTabHeaderClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is QueryTab tab)
        {
            if (DataContext is QueryTabsViewModel viewModel)
            {
                viewModel.SelectedTab = tab;
                
                // Focus the SQL editor after tab switch - delay to allow UI update
                Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusSqlEditor(), 
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    /// <summary>
    /// Tries to focus the SQL editor TextBox.
    /// </summary>
    private void FocusSqlEditor()
    {
        // Find the TextBox in the current visual tree
        var textBox = this.FindLogicalDescendantOfType<TextBox>();
        if (textBox != null)
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
        }
    }
}

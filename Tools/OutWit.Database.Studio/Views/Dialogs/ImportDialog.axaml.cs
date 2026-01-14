using Avalonia.Controls;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views.Dialogs;

/// <summary>
/// Dialog for importing data from files.
/// </summary>
public partial class ImportDialog : Window
{
    #region Constructors

    public ImportDialog()
    {
        InitializeComponent();
    }

    #endregion

    #region Functions

    /// <summary>
    /// Shows the import dialog and returns true if import was successful.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, ImportViewModel viewModel)
    {
        var dialog = new ImportDialog
        {
            DataContext = viewModel
        };

        var tcs = new TaskCompletionSource<bool>();

        viewModel.DialogClosed += result =>
        {
            tcs.TrySetResult(result);
            dialog.Close();
        };

        await dialog.ShowDialog(owner);
        
        return await tcs.Task;
    }

    #endregion
}

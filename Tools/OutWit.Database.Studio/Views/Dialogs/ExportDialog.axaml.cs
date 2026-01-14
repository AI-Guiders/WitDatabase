using Avalonia.Controls;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views.Dialogs;

/// <summary>
/// Dialog for exporting data to files.
/// </summary>
public partial class ExportDialog : Window
{
    #region Constructors

    public ExportDialog()
    {
        InitializeComponent();
    }

    #endregion

    #region Functions

    /// <summary>
    /// Shows the export dialog and returns true if export was successful.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, ExportViewModel viewModel)
    {
        var dialog = new ExportDialog
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

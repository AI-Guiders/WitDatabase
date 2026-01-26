using Avalonia.Controls;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views.Dialogs;

/// <summary>
/// About dialog showing application information and links.
/// </summary>
public partial class AboutDialog : Window
{
    #region Constructors

    public AboutDialog()
    {
        InitializeComponent();
    }

    #endregion

    #region Functions

    public static async Task ShowAsync(Window owner, AboutViewModel viewModel)
    {
        var dialog = new AboutDialog
        {
            DataContext = viewModel
        };

        viewModel.DialogClosed += () => dialog.Close();

        await dialog.ShowDialog(owner);
    }

    #endregion
}

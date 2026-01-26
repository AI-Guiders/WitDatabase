using Avalonia.Controls;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views.Dialogs;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner, SettingsViewModel viewModel)
    {
        var dialog = new SettingsDialog
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

        return tcs.Task.IsCompleted ? tcs.Task.Result : false;
    }
}

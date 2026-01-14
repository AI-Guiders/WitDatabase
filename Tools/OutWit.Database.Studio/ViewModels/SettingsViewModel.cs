using System.Collections.ObjectModel;
using System.Windows.Input;
using OutWit.Common.Aspects;
using OutWit.Common.MVVM.Commands;
using OutWit.Common.MVVM.ViewModels;
using OutWit.Database.Studio.Services;

namespace OutWit.Database.Studio.ViewModels;

/// <summary>
/// ViewModel for settings dialog.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase<ApplicationViewModel>
{
    #region Events

    public event Action<bool>? DialogClosed;

    #endregion

    #region Constructors

    public SettingsViewModel(ApplicationViewModel applicationVm)
        : base(applicationVm)
    {
        InitDefaults();
        InitCommands();
    }

    #endregion

    #region Initialization

    private void InitDefaults()
    {
        AvailableThemes = ["Light", "Dark"];
        AvailableFontSizes = [10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24];
    }

    private void InitCommands()
    {
        SaveCommand = new RelayCommandAsync(SaveAsync);
        CancelCommand = new RelayCommand(Cancel);
    }

    #endregion

    #region Functions

    public async Task InitializeAsync()
    {
        var settings = await Settings.LoadAsync();
        
        SelectedTheme = settings.Theme;
        EditorFontSize = settings.EditorFontSize;
        AutoSaveQueries = settings.AutoSaveQueries;
        MaxRecentFiles = settings.MaxRecentFiles;
    }

    private async Task SaveAsync()
    {
        var settings = await Settings.LoadAsync();
        
        var themeChanged = settings.Theme != SelectedTheme;
        
        settings.Theme = SelectedTheme;
        settings.EditorFontSize = EditorFontSize;
        settings.AutoSaveQueries = AutoSaveQueries;
        settings.MaxRecentFiles = MaxRecentFiles;
        
        await Settings.SaveAsync(settings);
        
        // Apply theme immediately if changed
        if (themeChanged)
        {
            App.Current?.ApplyTheme(SelectedTheme);
        }
        
        DialogClosed?.Invoke(true);
    }

    private void Cancel()
    {
        DialogClosed?.Invoke(false);
    }

    #endregion

    #region Properties

    public ObservableCollection<string> AvailableThemes { get; private set; } = null!;

    public ObservableCollection<int> AvailableFontSizes { get; private set; } = null!;

    [Notify]
    public string SelectedTheme { get; set; } = "Light";

    [Notify]
    public int EditorFontSize { get; set; } = 14;

    [Notify]
    public bool AutoSaveQueries { get; set; } = true;

    [Notify]
    public int MaxRecentFiles { get; set; } = 10;

    #endregion

    #region Commands

    public ICommand SaveCommand { get; private set; } = null!;

    public ICommand CancelCommand { get; private set; } = null!;

    #endregion

    #region Services

    private ISettingsService Settings => ApplicationVm.Settings;

    #endregion
}

using OutWit.Database.Studio.Services;
using Microsoft.Extensions.Logging;

namespace OutWit.Database.Studio.ViewModels;

/// <summary>
/// Main application view model that contains all other view models.
/// Acts as a container and communication hub for all ViewModels.
/// Singleton pattern for easy access throughout the application.
/// </summary>
public sealed class ApplicationViewModel
{
    #region Singleton

    private static readonly Lock LOCK = new();

    public static ApplicationViewModel Instance
    {
        get
        {
            if (field != null) 
                return field;

            lock (LOCK)
            {
                field ??= Program.GetService<ApplicationViewModel>();
            }
            return field;
        }
    }

    #endregion

    #region Constructors

    public ApplicationViewModel(
        IDatabaseService databaseService, 
        ISettingsService settingsService,
        IExportService exportService,
        ILogger<ApplicationViewModel> logger)
    {
        Database = databaseService;
        Settings = settingsService;
        Export = exportService;
        Logger = logger;

        InitViewModels();
    }

    #endregion

    #region Initialization

    private void InitViewModels()
    {
        MainWindowVm = new MainWindowViewModel(this);
        ConnectionVm = new ConnectionViewModel(this);
        DatabaseExplorerVm = new DatabaseExplorerViewModel(this);
        
        // Unified workspace tabs system
        WorkspaceTabsVm = new WorkspaceTabsViewModel(this);
        
        // Legacy ViewModel kept for backward compatibility with old tests
        QueryTabsVm = new QueryTabsViewModel(this);
    }

    #endregion

    #region Functions

    public ApplicationViewModel ResetOwnerWindow(Avalonia.Controls.Window? window)
    {
        MainWindow = window;
        return this;
    }

    #endregion

    #region View Models

    public MainWindowViewModel MainWindowVm { get; private set; } = null!;
    public ConnectionViewModel ConnectionVm { get; private set; } = null!;
    public DatabaseExplorerViewModel DatabaseExplorerVm { get; private set; } = null!;
    
    /// <summary>
    /// Unified workspace tabs system for Query, Edit, and Structure tabs.
    /// </summary>
    public WorkspaceTabsViewModel WorkspaceTabsVm { get; private set; } = null!;
    
    /// <summary>
    /// Legacy query tabs ViewModel - kept for backward compatibility with tests.
    /// Use WorkspaceTabsVm for new code.
    /// </summary>
    [Obsolete("Use WorkspaceTabsVm instead")]
    public QueryTabsViewModel QueryTabsVm { get; private set; } = null!;

    #endregion

    #region Properties

    public IDatabaseService Database { get; }

    public ISettingsService Settings { get; }

    public IExportService Export { get; }

    public ILogger<ApplicationViewModel> Logger { get; }
    
    public Avalonia.Controls.Window? MainWindow { get; private set; }

    #endregion
}

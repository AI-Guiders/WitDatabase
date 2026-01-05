using OutWit.Common.MVVM.ViewModels;
using OutWit.Common.MVVM.Commands;
using OutWit.Common.Aspects;
using OutWit.Database.Studio.Models;
using OutWit.Database.Studio.Services;
using Microsoft.Extensions.Logging;
using System.Data;

namespace OutWit.Database.Studio.ViewModels;

/// <summary>
/// ViewModel for SQL query editor.
/// </summary>
public class QueryEditorViewModel : ViewModelBase<ApplicationViewModel>
{
    #region Fields

    private readonly IDatabaseService m_databaseService;
    private readonly ILogger<QueryEditorViewModel> m_logger;
    private string m_selectedText = string.Empty;

    #endregion

    #region Constructors

    public QueryEditorViewModel(
        ApplicationViewModel applicationVm,
        IDatabaseService databaseService)
        : base(applicationVm)
    {
        m_databaseService = databaseService;
        m_logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<QueryEditorViewModel>.Instance;

        InitDefault();
        InitCommands();
    }

    #endregion

    #region Initialization

    private void InitDefault()
    {
        SqlText = string.Empty;
        StatusText = "Ready";
    }

    private void InitCommands()
    {
        ExecuteCommand = new DelegateCommand<object>(async _ => await ExecuteAsync(), _ => CanExecute());
        ExecuteSelectionCommand = new DelegateCommand<object>(async _ => await ExecuteSelectionAsync(), _ => CanExecuteSelection());
        ClearCommand = new DelegateCommand<object>(_ => Clear());
    }

    #endregion

    #region Commands

    public DelegateCommand<object> ExecuteCommand { get; private set; } = null!;
    public DelegateCommand<object> ExecuteSelectionCommand { get; private set; } = null!;
    public DelegateCommand<object> ClearCommand { get; private set; } = null!;

    private async Task ExecuteAsync()
    {
        await ExecuteQueryAsync(SqlText);
    }

    private async Task ExecuteSelectionAsync()
    {
        var textToExecute = string.IsNullOrWhiteSpace(m_selectedText) ? SqlText : m_selectedText;
        await ExecuteQueryAsync(textToExecute);
    }

    private async Task ExecuteQueryAsync(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;

        IsExecuting = true;
        ErrorMessage = null;
        Result = null;
        ResultDataView = null;

        try
        {
            var result = await m_databaseService.ExecuteQueryAsync(sql);
            
            Result = result;
            
            if (result.IsSuccess)
            {
                // Convert DataTable to DataView for binding
                if (result.ResultTable != null)
                {
                    ResultDataView = result.ResultTable.DefaultView;
                }

                RowsAffected = result.RowsAffected;
                ExecutionTimeMs = result.ExecutionTimeMs;
                StatusText = $"Query executed successfully in {result.ExecutionTimeMs:F2}ms";
                
                ApplicationVm.MainWindowVm.StatusText = 
                    $"Query executed in {result.ExecutionTimeMs:F2}ms. {result.RowsAffected} rows affected.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                StatusText = "Query execution failed";
                ApplicationVm.MainWindowVm.StatusText = "Query execution failed";
            }

            m_logger.LogInformation("Query executed: {Time}ms, {Rows} rows", 
                result.ExecutionTimeMs, result.RowsAffected);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Execution error: {ex.Message}";
            StatusText = "Query execution error";
            ApplicationVm.MainWindowVm.StatusText = "Query execution error";
            m_logger.LogError(ex, "Query execution failed");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private bool CanExecute()
    {
        return !string.IsNullOrWhiteSpace(SqlText) 
            && !IsExecuting 
            && m_databaseService.IsConnected;
    }

    private bool CanExecuteSelection()
    {
        return !IsExecuting && m_databaseService.IsConnected;
    }

    private void Clear()
    {
        SqlText = string.Empty;
        Result = null;
        ResultDataView = null;
        ErrorMessage = null;
        StatusText = "Ready";
        RowsAffected = 0;
        ExecutionTimeMs = 0;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the selected text from the editor.
    /// </summary>
    public void SetSelectedText(string selectedText)
    {
        m_selectedText = selectedText;
        ExecuteSelectionCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region Properties

    [Notify]
    public string SqlText { get; set; } = null!;

    [Notify]
    public string StatusText { get; set; } = null!;

    [Notify]
    public bool IsExecuting { get; set; }

    [Notify]
    public QueryResult? Result { get; set; }

    [Notify]
    public DataView? ResultDataView { get; set; }

    [Notify]
    public string? ErrorMessage { get; set; }

    [Notify]
    public int RowsAffected { get; set; }

    [Notify]
    public double ExecutionTimeMs { get; set; }

    public bool HasResults => ResultDataView != null && ResultDataView.Count > 0;
    public bool IsSuccess => Result != null && Result.IsSuccess;
    public bool HasMessages => IsSuccess || ErrorMessage != null;

    #endregion
}

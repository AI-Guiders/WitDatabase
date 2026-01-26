using NUnit.Framework;
using OutWit.Database.Studio.ViewModels;
using OutWit.Database.Studio.Tests.Helpers;

namespace OutWit.Database.Studio.Tests.ViewModels;

/// <summary>
/// Tests for QueryTabsViewModel.
/// </summary>
[TestFixture]
public class QueryTabsViewModelTests
{
    #region Fields

    private ApplicationViewModel m_applicationVm = null!;
    private QueryTabsViewModel m_viewModel = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        var databaseService = new FakeDatabaseService();
        m_applicationVm = new ApplicationViewModel(
            databaseService,
            new FakeSettingsService(),
            new FakeExportService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ApplicationViewModel>.Instance);
        
        m_viewModel = m_applicationVm.QueryTabsVm;
    }

    #endregion

    #region Initialization Tests

    [Test]
    public void InitialStateHasOneTabTest()
    {
        // Assert
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_viewModel.SelectedTab, Is.Not.Null);
        Assert.That(m_viewModel.SelectedTab?.Title, Does.StartWith("Query"));
    }

    #endregion

    #region NewTabCommand Tests

    [Test]
    public void NewTabCommandAddsNewTabTest()
    {
        // Act
        m_viewModel.NewTabCommand.Execute(null);

        // Assert
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(2));
        Assert.That(m_viewModel.SelectedTab, Is.EqualTo(m_viewModel.Tabs[1]));
    }

    #endregion

    #region CloseTabCommand Tests

    [Test]
    public void CloseTabCommandClosesTabTest()
    {
        // Arrange
        m_viewModel.NewTabCommand.Execute(null);
        var firstTab = m_viewModel.Tabs[0];
        var secondTab = m_viewModel.Tabs[1];

        // Act
        m_viewModel.CloseTabCommand.Execute(firstTab);

        // Assert
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_viewModel.Tabs, Does.Not.Contain(firstTab));
        Assert.That(m_viewModel.SelectedTab, Is.EqualTo(secondTab));
    }

    [Test]
    public void CloseTabCommandDoesNothingWhenOnlyOneTabTest()
    {
        // Arrange - only one tab exists
        var onlyTab = m_viewModel.Tabs[0];

        // Act
        m_viewModel.CloseTabCommand.Execute(onlyTab);

        // Assert - tab should remain (CanCloseTab is false with only 1 tab)
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_viewModel.Tabs[0], Is.EqualTo(onlyTab));
    }

    #endregion

    #region CurrentSqlText Tests

    [Test]
    public void CurrentSqlTextReturnsSelectedTabTextTest()
    {
        // Arrange
        const string testSql = "SELECT * FROM Users";
        m_viewModel.SelectedTab!.SqlText = testSql;

        // Act
        var result = m_viewModel.CurrentSqlText;

        // Assert
        Assert.That(result, Is.EqualTo(testSql));
    }

    [Test]
    public void CurrentSqlTextSetUpdatesSelectedTabTest()
    {
        // Arrange
        const string testSql = "SELECT * FROM Orders";

        // Act
        m_viewModel.CurrentSqlText = testSql;

        // Assert
        Assert.That(m_viewModel.SelectedTab?.SqlText, Is.EqualTo(testSql));
        Assert.That(m_viewModel.SelectedTab?.IsModified, Is.True);
    }

    #endregion

    #region MarkCurrentTabAsModified Tests

    [Test]
    public void MarkCurrentTabAsModifiedSetsModifiedFlagTest()
    {
        // Arrange
        m_viewModel.SelectedTab!.IsModified = false;

        // Act
        m_viewModel.MarkCurrentTabAsModified();

        // Assert
        Assert.That(m_viewModel.SelectedTab.IsModified, Is.True);
    }

    #endregion

    #region ExecuteQueryCommand Tests

    [Test]
    public void ExecuteQueryCommandDoesNothingWhenNotConnectedTest()
    {
        // Arrange
        var tab = m_viewModel.SelectedTab!;
        tab.SqlText = "SELECT 1";
        tab.ErrorMessage = null;

        // Act - FakeDatabaseService is not connected, so query won't execute
        m_viewModel.ExecuteQueryCommand.Execute(tab);

        // Assert - no error message because execution is skipped when not connected
        Assert.That(tab.ErrorMessage, Is.Null);
    }

    #endregion

    #region ClearResultsCommand Tests

    [Test]
    public void ClearResultsCommandClearsResultsTest()
    {
        // Arrange
        var tab = m_viewModel.SelectedTab!;
        tab.ErrorMessage = "Some error";
        tab.RowsAffected = 10;
        tab.ExecutionTimeMs = 100;

        // Act
        m_viewModel.ClearResultsCommand.Execute(tab);

        // Assert
        Assert.That(tab.ErrorMessage, Is.Null);
        Assert.That(tab.RowsAffected, Is.EqualTo(0));
        Assert.That(tab.ExecutionTimeMs, Is.EqualTo(0));
    }

    #endregion

    #region CanExecuteQuery Tests

    [Test]
    public void CanExecuteQueryIsFalseWhenNotConnectedTest()
    {
        // Arrange
        m_viewModel.SelectedTab!.SqlText = "SELECT 1";

        // Assert - FakeDatabaseService.IsConnected = false
        Assert.That(m_viewModel.CanExecuteQuery, Is.False);
    }

    [Test]
    public void CanExecuteQueryIsFalseWhenNoSqlTextTest()
    {
        // Arrange
        m_viewModel.SelectedTab!.SqlText = string.Empty;

        // Assert
        Assert.That(m_viewModel.CanExecuteQuery, Is.False);
    }

    #endregion
}

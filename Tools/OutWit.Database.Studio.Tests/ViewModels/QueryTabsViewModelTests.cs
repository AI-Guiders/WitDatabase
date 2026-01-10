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
    private ApplicationViewModel m_applicationVm = null!;
    private QueryTabsViewModel m_viewModel = null!;

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

    [Test]
    public void InitialState_HasOneTab()
    {
        // Assert
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_viewModel.SelectedTab, Is.Not.Null);
        Assert.That(m_viewModel.SelectedTab?.Title, Does.StartWith("Query"));
    }

    [Test]
    public void NewTabCommand_AddsNewTab()
    {
        // Act
        m_viewModel.NewTabCommand.Execute(null);

        // Assert
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(2));
        Assert.That(m_viewModel.SelectedTab, Is.EqualTo(m_viewModel.Tabs[1]));
    }

    [Test]
    public void CloseTabCommand_ClosesTab()
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
    public void CloseTabCommand_KeepsAtLeastOneTab()
    {
        // Arrange
        var onlyTab = m_viewModel.Tabs[0];

        // Act
        m_viewModel.CloseTabCommand.Execute(onlyTab);

        // Assert - should create new tab
        Assert.That(m_viewModel.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_viewModel.Tabs[0], Is.Not.EqualTo(onlyTab));
    }

    [Test]
    public void CurrentSqlText_ReturnsSelectedTabText()
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
    public void CurrentSqlText_Set_UpdatesSelectedTab()
    {
        // Arrange
        const string testSql = "SELECT * FROM Orders";

        // Act
        m_viewModel.CurrentSqlText = testSql;

        // Assert
        Assert.That(m_viewModel.SelectedTab?.SqlText, Is.EqualTo(testSql));
        Assert.That(m_viewModel.SelectedTab?.IsModified, Is.True);
    }

    [Test]
    public void MarkCurrentTabAsModified_SetsModifiedFlag()
    {
        // Arrange
        m_viewModel.SelectedTab!.IsModified = false;

        // Act
        m_viewModel.MarkCurrentTabAsModified();

        // Assert
        Assert.That(m_viewModel.SelectedTab.IsModified, Is.True);
    }

    [Test]
    public void ExecuteQueryCommand_ExecutesQuery()
    {
        // Arrange
        var tab = m_viewModel.SelectedTab!;
        tab.SqlText = "SELECT 1";

        // Act
        m_viewModel.ExecuteQueryCommand.Execute(tab);

        // Assert - Error message expected since fake service returns error
        Assert.That(tab.ErrorMessage, Is.Not.Null);
    }

    [Test]
    public void ClearResultsCommand_ClearsResults()
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
}

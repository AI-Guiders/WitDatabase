using Microsoft.Extensions.Logging.Abstractions;
using OutWit.Database.Studio.Tests.Helpers;
using OutWit.Database.Studio.ViewModels;
using OutWit.Database.Studio.ViewModels.Tabs;

namespace OutWit.Database.Studio.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="WorkspaceTabsViewModel"/>.
/// </summary>
[TestFixture]
public class WorkspaceTabsViewModelTests
{
    #region Fields

    private ApplicationViewModel m_appVm = null!;
    private WorkspaceTabsViewModel m_workspaceVm = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_appVm = new ApplicationViewModel(
            new FakeDatabaseService(),
            new FakeSettingsService(),
            new FakeExportService(),
            NullLogger<ApplicationViewModel>.Instance);

        m_workspaceVm = m_appVm.WorkspaceTabsVm;
    }

    #endregion

    #region Initial State Tests

    [Test]
    public void InitialStateHasOneQueryTabTest()
    {
        Assert.That(m_workspaceVm.Tabs, Has.Count.EqualTo(1));
        Assert.That(m_workspaceVm.SelectedTab, Is.InstanceOf<QueryTabViewModel>());
    }

    [Test]
    public void SelectedTabIsNotNullTest()
    {
        Assert.That(m_workspaceVm.SelectedTab, Is.Not.Null);
    }

    [Test]
    public void CanExecuteQueryIsFalseInitiallyTest()
    {
        // Not connected and no SQL text
        Assert.That(m_workspaceVm.CanExecuteQuery, Is.False);
    }

    #endregion

    #region Tab Management Tests

    [Test]
    public void NewQueryTabCommandCreatesNewTabTest()
    {
        var initialCount = m_workspaceVm.Tabs.Count;

        m_workspaceVm.NewQueryTabCommand.Execute(null);

        Assert.That(m_workspaceVm.Tabs, Has.Count.EqualTo(initialCount + 1));
    }

    [Test]
    public void OpenQueryTabReturnsNewTabTest()
    {
        var tab = m_workspaceVm.OpenQueryTab("SELECT 1", "Test Query");

        Assert.That(tab, Is.Not.Null);
        Assert.That(tab.SqlText, Is.EqualTo("SELECT 1"));
        Assert.That(tab.Title, Is.EqualTo("Test Query"));
        Assert.That(m_workspaceVm.SelectedTab, Is.EqualTo(tab));
    }

    [Test]
    public void CloseTabRemovesTabTest()
    {
        // Add a second tab
        m_workspaceVm.NewQueryTabCommand.Execute(null);
        var initialCount = m_workspaceVm.Tabs.Count;
        var tabToClose = m_workspaceVm.SelectedTab;

        m_workspaceVm.CloseTabCommand.Execute(tabToClose);

        Assert.That(m_workspaceVm.Tabs, Has.Count.EqualTo(initialCount - 1));
        Assert.That(m_workspaceVm.Tabs, Does.Not.Contain(tabToClose));
    }

    [Test]
    public void CannotCloseLastTabTest()
    {
        // Ensure only one tab
        while (m_workspaceVm.Tabs.Count > 1)
        {
            m_workspaceVm.CloseTabCommand.Execute(m_workspaceVm.Tabs.Last());
        }

        var lastTab = m_workspaceVm.SelectedTab;
        m_workspaceVm.CloseTabCommand.Execute(lastTab);

        // Tab should still be there
        Assert.That(m_workspaceVm.Tabs, Has.Count.EqualTo(1));
    }

    [Test]
    public void PinTabMovesToPinnedSectionTest()
    {
        // Create two tabs
        m_workspaceVm.NewQueryTabCommand.Execute(null);
        var tabToPin = m_workspaceVm.Tabs.Last();

        m_workspaceVm.PinTabCommand.Execute(tabToPin);

        Assert.That(tabToPin.IsPinned, Is.True);
        Assert.That(m_workspaceVm.Tabs.IndexOf(tabToPin), Is.EqualTo(0), "Pinned tab should be at the beginning");
    }

    [Test]
    public void UnpinTabMovesAfterPinnedTabsTest()
    {
        // Create and pin a tab
        m_workspaceVm.NewQueryTabCommand.Execute(null);
        var tabToPin = m_workspaceVm.Tabs.Last();
        m_workspaceVm.PinTabCommand.Execute(tabToPin);

        // Now unpin
        m_workspaceVm.UnpinTabCommand.Execute(tabToPin);

        Assert.That(tabToPin.IsPinned, Is.False);
    }

    #endregion

    #region DDL Detection Tests

    [Test]
    [TestCase("CREATE TABLE t (id INT)", true)]
    [TestCase("DROP TABLE t", true)]
    [TestCase("ALTER TABLE t ADD COLUMN x INT", true)]
    [TestCase("TRUNCATE TABLE t", true)]
    [TestCase("RENAME TABLE t TO t2", true)]
    [TestCase("SELECT * FROM t", false)]
    [TestCase("INSERT INTO t VALUES (1)", false)]
    [TestCase("UPDATE t SET x = 1", false)]
    [TestCase("DELETE FROM t", false)]
    [TestCase("-- comment\nCREATE TABLE t (id INT)", true)]
    [TestCase("/* block comment */\nDROP TABLE t", true)]
    [TestCase("-- comment\n-- another\nALTER TABLE t ADD x INT", true)]
    [TestCase("/* multi\nline\ncomment */CREATE TABLE t (id INT)", true)]
    [TestCase("  \n  \t  CREATE TABLE t (id INT)", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("-- only comment", false)]
    public void IsDdlStatementTest(string sql, bool expected)
    {
        // Use reflection to test private method
        var method = typeof(WorkspaceTabsViewModel).GetMethod(
            "IsDdlStatement",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [sql])!;

        Assert.That(result, Is.EqualTo(expected), $"SQL: {sql}");
    }

    #endregion
}

using OutWit.Database.Studio.ViewModels;
using OutWit.Database.Studio.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace OutWit.Database.Studio.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ApplicationViewModel"/>.
/// </summary>
[TestFixture]
public class ApplicationViewModelTests
{
    #region Fields

    private ApplicationViewModel m_appVm = null!;

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
    }

    #endregion

    #region ViewModels Tests

    [Test]
    public void ApplicationViewModelIsNotNullTest()
    {
        Assert.That(m_appVm, Is.Not.Null);
    }

    [Test]
    public void MainWindowVmIsNotNullTest()
    {
        Assert.That(m_appVm.MainWindowVm, Is.Not.Null);
    }

    [Test]
    public void ConnectionVmIsNotNullTest()
    {
        Assert.That(m_appVm.ConnectionVm, Is.Not.Null);
    }

    [Test]
    public void DatabaseExplorerVmIsNotNullTest()
    {
        Assert.That(m_appVm.DatabaseExplorerVm, Is.Not.Null);
    }

    [Test]
    public void QueryTabsVmIsNotNullTest()
    {
        Assert.That(m_appVm.QueryTabsVm, Is.Not.Null);
    }

    [Test]
    public void TableStructureVmIsNotNullTest()
    {
        Assert.That(m_appVm.TableStructureVm, Is.Not.Null);
    }

    #endregion

    #region Child ViewModels Tests

    [Test]
    public void AllChildViewModelsAreInitializedTest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(m_appVm.MainWindowVm, Is.Not.Null);
            Assert.That(m_appVm.ConnectionVm, Is.Not.Null);
            Assert.That(m_appVm.DatabaseExplorerVm, Is.Not.Null);
            Assert.That(m_appVm.QueryTabsVm, Is.Not.Null);
            Assert.That(m_appVm.TableStructureVm, Is.Not.Null);
        });
    }

    #endregion
}

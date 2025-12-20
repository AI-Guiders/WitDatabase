using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Stores;

/// <summary>
/// Tests BTreeStore with FileStorage using the common IKeyValueStore test suite.
/// </summary>
[TestFixture]
public class BTreeStoreFileTest : KeyValueStoreTestBase
{
    private string? m_testDir;
    private string? m_dbPath;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"BTreeStoreFileTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (m_testDir != null && Directory.Exists(m_testDir))
        {
            try { Directory.Delete(m_testDir, recursive: true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    protected override IKeyValueStore CreateStore()
    {
        m_dbPath = Path.Combine(m_testDir!, $"test_{Guid.NewGuid():N}.db");
        return new BTreeStore(m_dbPath, pageSize: 4096, cacheSize: 1000);
    }

    protected override void CleanupStore()
    {
        if (m_dbPath != null && File.Exists(m_dbPath))
        {
            try { File.Delete(m_dbPath); }
            catch { /* Ignore */ }
        }
    }
}

using OutWit.Database.AdoNet;
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;

namespace OutWit.Database.AdoNet.Tests.Parallel;

/// <summary>
/// Tests for ADO.NET parallel mode integration via connection string.
/// </summary>
[TestFixture]
public class WitDbConnectionParallelModeTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"adonet_parallel_mode_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    #endregion

    #region Connection String Parsing Tests

    [Test]
    public void ConnectionStringWithParallelModeAutoTest()
    {
        var cs = new WitDbConnectionStringBuilder
        {
            DataSource = Path.Combine(m_testDir, "test.witdb"),
            ParallelMode = WitDbParallelMode.Auto
        };

        Assert.That(cs.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
        Assert.That(cs.ConnectionString, Does.Contain("Parallel Mode=Auto"));
    }

    [Test]
    public void ConnectionStringWithParallelModeLatchedTest()
    {
        var cs = new WitDbConnectionStringBuilder
        {
            DataSource = Path.Combine(m_testDir, "test.witdb"),
            ParallelMode = WitDbParallelMode.Latched
        };

        Assert.That(cs.ParallelMode, Is.EqualTo(WitDbParallelMode.Latched));
    }

    [Test]
    public void ConnectionStringWithMaxWritersTest()
    {
        var cs = new WitDbConnectionStringBuilder
        {
            DataSource = Path.Combine(m_testDir, "test.witdb"),
            ParallelMode = WitDbParallelMode.Auto,
            MaxWriters = 8
        };

        Assert.That(cs.MaxWriters, Is.EqualTo(8));
        Assert.That(cs.ConnectionString, Does.Contain("Max Writers=8"));
    }

    [Test]
    public void ParseConnectionStringWithParallelModeTest()
    {
        var dbPath = Path.Combine(m_testDir, "test.witdb");
        var cs = new WitDbConnectionStringBuilder($"Data Source={dbPath};Parallel Mode=Buffered;Max Writers=4");

        Assert.That(cs.ParallelMode, Is.EqualTo(WitDbParallelMode.Buffered));
        Assert.That(cs.MaxWriters, Is.EqualTo(4));
    }

    #endregion

    #region Connection Open Tests

    [Test]
    public void OpenConnectionWithParallelModeAutoTest()
    {
        var dbPath = Path.Combine(m_testDir, "parallel_auto.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Auto;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        Assert.That(conn.State, Is.EqualTo(System.Data.ConnectionState.Open));
        
        // Test basic operation
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INTEGER PRIMARY KEY)";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void OpenConnectionWithParallelModeLatchedTest()
    {
        var dbPath = Path.Combine(m_testDir, "parallel_latched.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Latched;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        Assert.That(conn.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    [Test]
    public void OpenConnectionWithParallelModeBufferedTest()
    {
        var dbPath = Path.Combine(m_testDir, "parallel_buffered.witdb");
        var cs = $"Data Source={dbPath};Store=lsm;Parallel Mode=Buffered;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        Assert.That(conn.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    #endregion

    #region SQL Operations Tests

    [Test]
    public void InsertAndSelectWithParallelModeTest()
    {
        var dbPath = Path.Combine(m_testDir, "insert_select.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Auto;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Products (Id INTEGER PRIMARY KEY, Name TEXT, Price REAL)";
            cmd.ExecuteNonQuery();
        }

        // Insert data
        for (int i = 1; i <= 10; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO Products (Id, Name, Price) VALUES ({i}, 'Product{i}', {i * 10.5})";
            cmd.ExecuteNonQuery();
        }

        // Select and verify
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Products";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.That(count, Is.EqualTo(10));
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Name FROM Products WHERE Id = 5";
            var name = cmd.ExecuteScalar()?.ToString();
            Assert.That(name, Is.EqualTo("Product5"));
        }
    }

    [Test]
    public void UpdateAndDeleteWithParallelModeTest()
    {
        var dbPath = Path.Combine(m_testDir, "update_delete.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Latched;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value TEXT)";
            cmd.ExecuteNonQuery();
        }

        // Insert
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Items (Id, Value) VALUES (1, 'Original')";
            cmd.ExecuteNonQuery();
        }

        // Update
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Items SET Value = 'Modified' WHERE Id = 1";
            var affected = cmd.ExecuteNonQuery();
            Assert.That(affected, Is.EqualTo(1));
        }

        // Verify update
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Value FROM Items WHERE Id = 1";
            var value = cmd.ExecuteScalar()?.ToString();
            Assert.That(value, Is.EqualTo("Modified"));
        }

        // Delete
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Items WHERE Id = 1";
            var affected = cmd.ExecuteNonQuery();
            Assert.That(affected, Is.EqualTo(1));
        }

        // Verify delete
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Items";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.That(count, Is.EqualTo(0));
        }
    }

    #endregion

    #region Multiple Connection Tests

    [Test]
    public void MultipleConnectionsSameFileTest()
    {
        var dbPath = Path.Combine(m_testDir, "multi_conn.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Auto;Transactions=false";

        // First connection - create table
        using (var conn1 = new WitDbConnection(cs))
        {
            conn1.Open();
            using var cmd = conn1.CreateCommand();
            cmd.CommandText = "CREATE TABLE Shared (Id INTEGER PRIMARY KEY, Data TEXT)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO Shared (Id, Data) VALUES (1, 'FromConn1')";
            cmd.ExecuteNonQuery();
        }

        // Second connection - read and write
        using (var conn2 = new WitDbConnection(cs))
        {
            conn2.Open();
            
            using var readCmd = conn2.CreateCommand();
            readCmd.CommandText = "SELECT Data FROM Shared WHERE Id = 1";
            var data = readCmd.ExecuteScalar()?.ToString();
            Assert.That(data, Is.EqualTo("FromConn1"));

            using var writeCmd = conn2.CreateCommand();
            writeCmd.CommandText = "INSERT INTO Shared (Id, Data) VALUES (2, 'FromConn2')";
            writeCmd.ExecuteNonQuery();
        }

        // Third connection - verify all data
        using (var conn3 = new WitDbConnection(cs))
        {
            conn3.Open();
            using var cmd = conn3.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Shared";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2));
        }
    }

    #endregion

    #region DataReader Tests

    [Test]
    public void DataReaderWithParallelModeTest()
    {
        var dbPath = Path.Combine(m_testDir, "datareader.witdb");
        var cs = $"Data Source={dbPath};Parallel Mode=Auto;Transactions=false";

        using var conn = new WitDbConnection(cs);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Records (Id INTEGER PRIMARY KEY, Name TEXT)";
            cmd.ExecuteNonQuery();
        }

        for (int i = 1; i <= 5; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO Records (Id, Name) VALUES ({i}, 'Record{i}')";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name FROM Records ORDER BY Id";
            using var reader = cmd.ExecuteReader();
            
            int count = 0;
            while (reader.Read())
            {
                count++;
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                Assert.That(name, Is.EqualTo($"Record{id}"));
            }

            Assert.That(count, Is.EqualTo(5));
        }
    }

    #endregion
}

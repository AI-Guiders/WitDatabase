using NUnit.Framework;
using OutWit.Database.AdoNet;
using System.Data;

namespace OutWit.Database.AdoNet.Tests;

// <summary>
/// Tests for WitDbConnection with various connection string configurations.
/// </summary>
[TestFixture]
public class WitDbConnectionTests
{
    private string? _testDbPath;
    private string? _testLsmPath;

    [SetUp]
    public void SetUp()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"WitDbConnectionTest_{Guid.NewGuid():N}.witdb");
        _testLsmPath = Path.Combine(Path.GetTempPath(), $"WitDbConnectionTest_LSM_{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        // Cleanup test files
        if (_testDbPath != null && File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
        
        if (_testLsmPath != null && Directory.Exists(_testLsmPath))
        {
            try { Directory.Delete(_testLsmPath, recursive: true); } catch { }
        }
    }

    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesClosedConnection()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(connection.ConnectionString, Is.Empty);
    }

    [Test]
    public void ConstructorWithConnectionStringSetsProperty()
    {
        var connectionString = "Data Source=:memory:";
        using var connection = new WitDbConnection(connectionString);

        Assert.That(connection.ConnectionString, Is.EqualTo(connectionString));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Open/Close Tests

    [Test]
    public void OpenWithMemoryDatabaseSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void OpenWithFileDatabaseCreatesFile()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath}");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        Assert.That(File.Exists(_testDbPath), Is.True);
    }

    [Test]
    public void CloseChangesStateToClose()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        
        connection.Close();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void OpenTwiceDoesNotThrow()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.DoesNotThrow(() => connection.Open());
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void CloseTwiceDoesNotThrow()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        connection.Close();

        Assert.DoesNotThrow(() => connection.Close());
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void OpenWithoutConnectionStringThrows()
    {
        using var connection = new WitDbConnection();

        Assert.Throws<InvalidOperationException>(() => connection.Open());
    }

    [Test]
    public async Task OpenAsyncWithMemoryDatabaseSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        await connection.OpenAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Connection Mode Tests

    [Test]
    public void MemoryModeCreatesInMemoryDatabase()
    {
        using var connection = new WitDbConnection("Mode=Memory");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void ReadWriteCreateModeCreatesFileIfNotExists()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Mode=ReadWriteCreate");
        
        Assert.That(File.Exists(_testDbPath), Is.False);
        connection.Open();
        Assert.That(File.Exists(_testDbPath), Is.True);
    }

    #endregion

    #region Encryption Tests

    [Test]
    public void AesGcmEncryptionWithPasswordSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Encryption=aes-gcm;Password=TestPassword123");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        
        // Write some data
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Name VARCHAR(100))";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void AesGcmEncryptionWithUserAndPasswordSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Encryption=aes-gcm;User=admin;Password=TestPassword123");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void FastEncryptionSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Encryption=aes-gcm;Password=TestPassword123;Fast Encryption=true");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void ChaCha20EncryptionThrowsHelpfulExceptionWhenNotRegistered()
    {
        // ChaCha20 requires BouncyCastle package which registers provider via ModuleInitializer
        // If the provider is not registered, we should get a helpful error message
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Encryption=chacha20-poly1305;Password=TestPassword123");

        var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());
        Assert.That(ex!.Message, Does.Contain("Encryption provider").Or.Contain("chacha20-poly1305").Or.Contain("not registered"));
    }

    [Test]
    public void CustomEncryptionProviderKeyThrowsForUnregisteredProvider()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Encryption=custom-algo;Password=TestPassword123");

        var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());
        Assert.That(ex!.Message, Does.Contain("custom-algo").Or.Contain("not registered"));
    }

    #endregion

    #region Store Engine Tests

    [Test]
    public void BTreeStoreSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Store=btree");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void LsmStoreSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testLsmPath};Store=lsm");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        Assert.That(Directory.Exists(_testLsmPath), Is.True);
    }

    [Test]
    public void LsmStoreWithCustomOptionsSucceeds()
    {
        var connectionString = $"Data Source={_testLsmPath};Store=lsm;LSM MemTable Size=8388608;LSM Block Size=4096;LSM WAL=true;LSM Sync=false;LSM Background Compaction=true";
        using var connection = new WitDbConnection(connectionString);
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void InMemoryStoreSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;Store=inmemory");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void CustomStoreProviderKeyThrowsForUnregisteredProvider()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Store=custom-store");

        var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());
        Assert.That(ex!.Message, Does.Contain("custom-store").Or.Contain("not registered"));
    }

    #endregion

    #region Transaction Tests

    [Test]
    public void BeginTransactionSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        Assert.That(transaction, Is.Not.Null);
    }

    [Test]
    public void BeginTransactionWithIsolationLevelSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction(IsolationLevel.Snapshot);

        Assert.That(transaction.IsolationLevel, Is.EqualTo(IsolationLevel.Snapshot));
    }

    [Test]
    public void TransactionCommitSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = (WitDbTransaction)connection.BeginTransaction();
        
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();
        
        transaction.Commit();

        // Verify table exists
        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void TransactionRollbackSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        // Create table first
        using var setupCmd = connection.CreateCommand();
        setupCmd.CommandText = "CREATE TABLE Test (Id INT)";
        setupCmd.ExecuteNonQuery();

        using var transaction = (WitDbTransaction)connection.BeginTransaction();
        
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        
        transaction.Rollback();

        // Verify data was not committed
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = checkCmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void MvccEnabledSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;MVCC=true;Isolation Level=Snapshot");
        connection.Open();

        using var transaction = connection.BeginTransaction(IsolationLevel.Snapshot);
        Assert.That(transaction, Is.Not.Null);
    }

    [Test]
    public void MvccDisabledSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;MVCC=false");
        connection.Open();

        using var transaction = connection.BeginTransaction();
        Assert.That(transaction, Is.Not.Null);
    }

    [Test]
    public void TransactionsDisabledSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;Transactions=false");
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Cache and Page Settings Tests

    [Test]
    public void CustomCacheSizeSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Cache Size=500");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void CustomPageSizeSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Page Size=8192");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Locking Settings Tests

    [Test]
    public void FileLockingEnabledSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};File Locking=true");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void FileLockingDisabledSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};File Locking=false");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void CustomLockTimeoutSucceeds()
    {
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Lock Timeout=60");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Properties Tests

    [Test]
    public void DataSourcePropertyReturnsCorrectValue()
    {
        using var connection = new WitDbConnection("Data Source=test.db");

        Assert.That(connection.DataSource, Is.EqualTo("test.db"));
    }

    [Test]
    public void DatabasePropertyReturnsFileNameWithoutExtension()
    {
        using var connection = new WitDbConnection("Data Source=mydata.witdb");

        Assert.That(connection.Database, Is.EqualTo("mydata"));
    }

    [Test]
    public void ServerVersionReturnsExpectedValue()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.ServerVersion, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void ChangingConnectionStringWhileOpenThrows()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.Throws<InvalidOperationException>(() => connection.ConnectionString = "Data Source=other.db");
    }

    #endregion

    #region Command Tests

    [Test]
    public void CreateCommandReturnsWitDbCommand()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();

        Assert.That(command, Is.InstanceOf<WitDbCommand>());
        Assert.That(command.Connection, Is.SameAs(connection));
    }

    [Test]
    public void ExecuteSimpleQuerySucceeds()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 + 1";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.EqualTo(2L));
    }

    #endregion

    #region ChangeDatabase Tests

    [Test]
    public void ChangeDatabaseToSameNameSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=mydb.witdb");
        connection.Open();

        Assert.DoesNotThrow(() => connection.ChangeDatabase("mydb"));
    }

    [Test]
    public void ChangeDatabaseToMainSucceeds()
    {
        using var connection = new WitDbConnection("Data Source=mydb.witdb");
        connection.Open();

        Assert.DoesNotThrow(() => connection.ChangeDatabase("main"));
    }

    [Test]
    public void ChangeDatabaseToDifferentNameThrows()
    {
        using var connection = new WitDbConnection("Data Source=mydb.witdb");
        connection.Open();

        Assert.Throws<NotSupportedException>(() => connection.ChangeDatabase("otherdb"));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeClosesConnection()
    {
        var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        
        connection.Dispose();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public async Task DisposeAsyncClosesConnection()
    {
        var connection = new WitDbConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        await connection.DisposeAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void DisposeRollsBackActiveTransaction()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();

        var transaction = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();

        // Dispose connection without committing
        connection.Dispose();

        // Transaction should have been rolled back
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Complex Scenario Tests

    [Test]
    public void FullWorkflowWithEncryptionAndMvcc()
    {
        var connectionString = $"Data Source={_testDbPath};Encryption=aes-gcm;Password=SecurePass123;MVCC=true;Isolation Level=Snapshot";
        
        // Create and populate database
        using (var connection = new WitDbConnection(connectionString))
        {
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE Users (Id INT PRIMARY KEY, Name VARCHAR(100))";
            cmd.ExecuteNonQuery();
            
            using var tx = (WitDbTransaction)connection.BeginTransaction(IsolationLevel.Snapshot);
            cmd.Transaction = tx;
            
            cmd.CommandText = "INSERT INTO Users VALUES (1, 'Alice')";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "INSERT INTO Users VALUES (2, 'Bob')";
            cmd.ExecuteNonQuery();
            
            tx.Commit();
        }

        // Reopen and verify
        using (var connection = new WitDbConnection(connectionString))
        {
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = cmd.ExecuteScalar();
            
            Assert.That(count, Is.EqualTo(2L));
        }
    }

    [Test]
    public void LsmStoreWithEncryptionWorkflow()
    {
        var connectionString = $"Data Source={_testLsmPath};Store=lsm;Encryption=aes-gcm;Password=LsmPassword;LSM MemTable Size=1048576";
        
        using var connection = new WitDbConnection(connectionString);
        connection.Open();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Data (Key VARCHAR(100) PRIMARY KEY, Value TEXT)";
        cmd.ExecuteNonQuery();
        
        // Insert multiple records to test LSM behavior
        for (int i = 0; i < 100; i++)
        {
            cmd.CommandText = $"INSERT INTO Data VALUES ('key{i}', 'value{i}')";
            cmd.ExecuteNonQuery();
        }
        
        cmd.CommandText = "SELECT COUNT(*) FROM Data";
        var count = cmd.ExecuteScalar();
        
        Assert.That(count, Is.EqualTo(100L));
    }

    [Test]
    public void UserPasswordEncryptionDifferentFromPasswordOnly()
    {
        var path1 = Path.Combine(Path.GetTempPath(), $"WitDb_PasswordOnly_{Guid.NewGuid():N}.witdb");
        var path2 = Path.Combine(Path.GetTempPath(), $"WitDb_UserPassword_{Guid.NewGuid():N}.witdb");
        
        try
        {
            // Create database with password only
            using (var conn1 = new WitDbConnection($"Data Source={path1};Encryption=aes-gcm;Password=SamePassword"))
            {
                conn1.Open();
                using var cmd = conn1.CreateCommand();
                cmd.CommandText = "CREATE TABLE Test (Id INT)";
                cmd.ExecuteNonQuery();
            }
            
            // Create database with user + password
            using (var conn2 = new WitDbConnection($"Data Source={path2};Encryption=aes-gcm;User=admin;Password=SamePassword"))
            {
                conn2.Open();
                using var cmd = conn2.CreateCommand();
                cmd.CommandText = "CREATE TABLE Test (Id INT)";
                cmd.ExecuteNonQuery();
            }
            
            // Verify files are different (different encryption keys due to different salt derivation)
            var bytes1 = File.ReadAllBytes(path1);
            var bytes2 = File.ReadAllBytes(path2);
            
            Assert.That(bytes1, Is.Not.EqualTo(bytes2));
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    #endregion

    #region Schema Tests

    [Test]
    public void GetSchemaReturnsMetaDataCollections()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        var schema = connection.GetSchema();

        Assert.That(schema, Is.Not.Null);
        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void GetSchemaTablesReturnsTableInfo()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestTable (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        var schema = connection.GetSchema("Tables");

        Assert.That(schema, Is.Not.Null);
    }

    #endregion

    #region Provider Key Tests

    [Test]
    public void StoreProviderKeysAreCaseInsensitive()
    {
        // Test uppercase
        using var conn1 = new WitDbConnection("Data Source=:memory:;Store=BTREE");
        conn1.Open();
        Assert.That(conn1.State, Is.EqualTo(ConnectionState.Open));
        conn1.Close();

        // Test lowercase
        using var conn2 = new WitDbConnection("Data Source=:memory:;Store=btree");
        conn2.Open();
        Assert.That(conn2.State, Is.EqualTo(ConnectionState.Open));
        conn2.Close();

        // Test mixed case
        using var conn3 = new WitDbConnection("Data Source=:memory:;Store=BTree");
        conn3.Open();
        Assert.That(conn3.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void EncryptionProviderKeysAreCaseInsensitive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"WitDb_CaseInsensitive_{Guid.NewGuid():N}.witdb");
        try
        {
            // Test uppercase
            using var conn1 = new WitDbConnection($"Data Source={path};Encryption=AES-GCM;Password=test");
            conn1.Open();
            Assert.That(conn1.State, Is.EqualTo(ConnectionState.Open));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    #endregion

    #region Default Values Tests

    [Test]
    public void DefaultConnectionStringUsesBuilderDefaults()
    {
        // Minimal connection string - should use all defaults
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        
        // Verify we can execute queries (means database is properly initialized)
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "SELECT Id FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();
        Assert.That(result, Is.EqualTo(1L));
    }

    [Test]
    public void DefaultStoreIsBTree()
    {
        // Verify btree is default - simply test that file-based database works
        using var connection = new WitDbConnection($"Data Source={_testDbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Value VARCHAR(100))";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'test')";
        cmd.ExecuteNonQuery();

        // Verify data is readable in the same session
        cmd.CommandText = "SELECT Value FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();
        Assert.That(result, Is.EqualTo("test"));
        
        // File should be created
        Assert.That(File.Exists(_testDbPath), Is.True);
    }

    [Test]
    public void DefaultMvccIsEnabled()
    {
        // Default MVCC=true - snapshot isolation should work
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        // Begin transaction with snapshot isolation (requires MVCC)
        using var tx = (WitDbTransaction)connection.BeginTransaction(IsolationLevel.Snapshot);
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        tx.Commit();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(1L));
    }

    [Test]
    public void DefaultTransactionsEnabled()
    {
        // Default Transactions=true - transactions should work
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        using var tx = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        
        // Rollback
        tx.Rollback();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(0L), "Rollback should have undone the insert");
    }

    [Test]
    public void DefaultFileLockingEnabled()
    {
        // Default FileLocking=true - file-based databases should use locking
        using var connection = new WitDbConnection($"Data Source={_testDbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void OnlySpecifiedSettingsOverrideDefaults()
    {
        // Only override cache size, everything else should use defaults
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Cache Size=100");
        connection.Open();

        // Should still have MVCC, transactions, file locking, etc.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        // Transactions should work (default enabled)
        using var tx = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        tx.Commit();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(1L));
    }

    [Test]
    public void OnlyOverridePageSizeKeepsOtherDefaults()
    {
        // Only override page size
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Page Size=8192");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Data VARCHAR(1000))";
        cmd.ExecuteNonQuery();

        // Insert some data to verify it works
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'test data')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Data FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();
        Assert.That(result, Is.EqualTo("test data"));
    }

    [Test]
    public void OnlyOverrideLockTimeoutKeepsOtherDefaults()
    {
        // Only override lock timeout
        using var connection = new WitDbConnection($"Data Source={_testDbPath};Lock Timeout=60");
        connection.Open();

        // Everything else should work with defaults
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        using var tx = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        tx.Commit();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void ExplicitlySettingDefaultValuesShouldWorkSameAsOmitting()
    {
        // Explicitly set all defaults - should work the same as minimal connection string
        // Note: Encryption is not set (default is no encryption)
        var explicitDefaults = $"Data Source={_testDbPath};Store=btree;MVCC=true;Transactions=true;Isolation Level=ReadCommitted";
        
        using var connection = new WitDbConnection(explicitDefaults);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        using var tx = (WitDbTransaction)connection.BeginTransaction(IsolationLevel.Snapshot);
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        tx.Commit();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(1L));
    }

    #endregion
}

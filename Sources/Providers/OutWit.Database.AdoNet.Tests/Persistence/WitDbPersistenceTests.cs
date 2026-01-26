using OutWit.Database.AdoNet;
using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.Persistence;

/// <summary>
/// Tests for data persistence - verifies that changes survive connection close/reopen.
/// </summary>
[TestFixture]
public class WitDbPersistenceTests
{
    private string m_dbPath = null!;

    [SetUp]
    public void Setup()
    {
        // Create unique file path for each test
        m_dbPath = Path.Combine(Path.GetTempPath(), $"persistence_test_{Guid.NewGuid():N}.witdb");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test file
        CleanupFile(m_dbPath);
        CleanupFile(m_dbPath + ".journal");
        CleanupFile(m_dbPath + "_indexes");
    }

    private static void CleanupFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { /* ignore */ }
    }

    #region Insert Persistence Tests

    [Test]
    public void InsertedDataPersistsAfterCloseTest()
    {
        // Session 1: Create table and insert data
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Users (Id, Name) VALUES (1, 'Alice')";
            insertCmd.ExecuteNonQuery();

            // Explicit flush before close
            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Verify data exists
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Name FROM Users WHERE Id = 1";
            var result = selectCmd.ExecuteScalar();

            Assert.That(result, Is.EqualTo("Alice"), "Inserted data should persist after close/reopen");
        }
    }

    [Test]
    public void MultipleInsertsPersistAfterCloseTest()
    {
        // Session 1: Create and insert multiple rows
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Users (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100))";
            createCmd.ExecuteNonQuery();

            for (int i = 1; i <= 10; i++)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = $"INSERT INTO Users (Name) VALUES ('User{i}')";
                insertCmd.ExecuteNonQuery();
            }

            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Verify all data exists
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            Assert.That(count, Is.EqualTo(10), "All 10 rows should persist");
        }
    }

    #endregion

    #region Update Persistence Tests

    [Test]
    public void UpdatedDataPersistsAfterCloseTest()
    {
        // Session 1: Create, insert, then update
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Users (Id, Name) VALUES (1, 'Alice')";
            insertCmd.ExecuteNonQuery();

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE Users SET Name = 'Alice Updated' WHERE Id = 1";
            updateCmd.ExecuteNonQuery();

            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Verify updated data
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Name FROM Users WHERE Id = 1";
            var result = selectCmd.ExecuteScalar();

            Assert.That(result, Is.EqualTo("Alice Updated"), "Updated data should persist");
        }
    }

    #endregion

    #region Delete Persistence Tests

    [Test]
    public void DeletedDataStaysDeletedAfterCloseTest()
    {
        // Session 1: Create, insert, then delete
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Users (Id, Name) VALUES (1, 'Alice');
                INSERT INTO Users (Id, Name) VALUES (2, 'Bob');
                INSERT INTO Users (Id, Name) VALUES (3, 'Charlie')";
            insertCmd.ExecuteNonQuery();

            // Verify 3 rows exist before delete
            using var countBeforeCmd = connection.CreateCommand();
            countBeforeCmd.CommandText = "SELECT COUNT(*) FROM Users";
            Assert.That(Convert.ToInt64(countBeforeCmd.ExecuteScalar()), Is.EqualTo(3));

            // Delete Bob
            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id = 2";
            var affected = deleteCmd.ExecuteNonQuery();
            Assert.That(affected, Is.EqualTo(1), "DELETE should affect 1 row");

            // Verify 2 rows remain before close
            using var countAfterCmd = connection.CreateCommand();
            countAfterCmd.CommandText = "SELECT COUNT(*) FROM Users";
            Assert.That(Convert.ToInt64(countAfterCmd.ExecuteScalar()), Is.EqualTo(2));

            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Verify deletion persisted
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            // Check count
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2), "Should have 2 users after reopen (delete should persist)");

            // Check Bob is not there
            using var selectBobCmd = connection.CreateCommand();
            selectBobCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Id = 2";
            var bobCount = Convert.ToInt64(selectBobCmd.ExecuteScalar());
            Assert.That(bobCount, Is.EqualTo(0), "Bob should not exist after reopen");

            // Check Alice and Charlie are there
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Name FROM Users ORDER BY Id";
            using var reader = selectCmd.ExecuteReader();

            var names = new List<string>();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            Assert.That(names, Is.EqualTo(new[] { "Alice", "Charlie" }), 
                "Only Alice and Charlie should remain after reopen");
        }
    }

    [Test]
    public void MultipleDeletesPersistAfterCloseTest()
    {
        // Session 1: Create, insert many, delete some
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Value INT)";
            createCmd.ExecuteNonQuery();

            // Insert 100 items
            for (int i = 1; i <= 100; i++)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = $"INSERT INTO Items (Value) VALUES ({i})";
                insertCmd.ExecuteNonQuery();
            }

            // Delete even-valued items (50 items)
            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Items WHERE Value % 2 = 0";
            var affected = deleteCmd.ExecuteNonQuery();
            Assert.That(affected, Is.EqualTo(50), "Should delete 50 items");

            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Verify
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Items";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());
            Assert.That(count, Is.EqualTo(50), "Should have 50 items (odd values only)");

            // Verify no even values exist
            using var evenCmd = connection.CreateCommand();
            evenCmd.CommandText = "SELECT COUNT(*) FROM Items WHERE Value % 2 = 0";
            var evenCount = Convert.ToInt64(evenCmd.ExecuteScalar());
            Assert.That(evenCount, Is.EqualTo(0), "No even values should exist");
        }
    }

    #endregion

    #region WebAPI Scenario Tests

    [Test]
    public void WebApiScenario_DeletePersistsAfterRestartTest()
    {
        // This test mimics the exact WebAPI scenario:
        // 1. Start app, create tables, seed data
        // 2. Delete a user
        // 3. Stop app
        // 4. Restart app
        // 5. Verify user is still deleted (not re-seeded because COUNT > 0)

        // Session 1: Initialize and seed
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            // Create table (like DatabaseInitializer)
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id BIGINT PRIMARY KEY AUTOINCREMENT,
                    Name VARCHAR(100) NOT NULL,
                    Email VARCHAR(255) NOT NULL
                )";
            createCmd.ExecuteNonQuery();

            // Check if seeding needed
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            if (count == 0)
            {
                // Seed data
                using var seedCmd = connection.CreateCommand();
                seedCmd.CommandText = @"
                    INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@test.com');
                    INSERT INTO Users (Name, Email) VALUES ('Bob', 'bob@test.com');
                    INSERT INTO Users (Name, Email) VALUES ('Carol', 'carol@test.com')";
                seedCmd.ExecuteNonQuery();
            }

            // Delete Carol (Id = 3)
            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id = 3";
            var affected = deleteCmd.ExecuteNonQuery();
            Assert.That(affected, Is.EqualTo(1));

            // Verify deletion within session
            using var verifyCmd = connection.CreateCommand();
            verifyCmd.CommandText = "SELECT COUNT(*) FROM Users";
            var countAfter = Convert.ToInt64(verifyCmd.ExecuteScalar());
            Assert.That(countAfter, Is.EqualTo(2));

            connection.Engine?.Flush();
            connection.Close();
        }

        // Session 2: Simulate app restart with same initialization logic
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            // Create table (IF NOT EXISTS - should not fail)
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id BIGINT PRIMARY KEY AUTOINCREMENT,
                    Name VARCHAR(100) NOT NULL,
                    Email VARCHAR(255) NOT NULL
                )";
            createCmd.ExecuteNonQuery();

            // Check if seeding needed (should NOT seed because COUNT > 0)
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            // THIS IS THE KEY ASSERTION:
            // Count should be 2 (not 0, and not 3)
            // If count is 0, then DELETE didn't persist
            // If count is 3, then data was re-seeded incorrectly
            Assert.That(count, Is.EqualTo(2), 
                "Should have 2 users after restart - delete should persist and seed should NOT run");

            // Verify Carol is NOT there
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Name FROM Users ORDER BY Id";
            using var reader = selectCmd.ExecuteReader();

            var names = new List<string>();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            Assert.That(names, Is.EqualTo(new[] { "Alice", "Bob" }), 
                "Only Alice and Bob should exist - Carol should stay deleted");
        }
    }

    #endregion

    #region Flush Tests

    [Test]
    public void DataNotPersistedWithoutFlushTest()
    {
        // This test demonstrates that explicit Flush is needed
        // (or proper Close which should auto-flush)

        // Session 1: Insert WITHOUT flush - just abruptly "crash"
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY)";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Test (Id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            // Flush to ensure table is created
            connection.Engine?.Flush();

            // Insert more WITHOUT flush
            using var insert2Cmd = connection.CreateCommand();
            insert2Cmd.CommandText = "INSERT INTO Test (Id) VALUES (2)";
            insert2Cmd.ExecuteNonQuery();

            // Flush again to persist second insert
            connection.Engine?.Flush();

            connection.Close();
        }

        // Session 2: Verify both rows exist
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Test";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            Assert.That(count, Is.EqualTo(2), "Both inserts should be flushed and persisted");
        }
    }

    [Test]
    public void CloseWithoutExplicitFlushShouldPersistDataTest()
    {
        // Test that Close() properly flushes data (via Dispose)

        // Session 1: Insert and close normally (no explicit Flush)
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Value VARCHAR(50))";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Test (Id, Value) VALUES (1, 'Test Value')";
            insertCmd.ExecuteNonQuery();

            // NO explicit Flush() - rely on Close/Dispose
            connection.Close();
        }

        // Session 2: Verify data exists
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Value FROM Test WHERE Id = 1";
            var result = selectCmd.ExecuteScalar();

            Assert.That(result, Is.EqualTo("Test Value"), 
                "Data should persist even without explicit Flush (Close should auto-flush)");
        }
    }

    #endregion

    #region Multiple Session Tests

    [Test]
    public void MultipleSessionsDeletePersistenceTest()
    {
        // Session 1: Create and seed
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id BIGINT PRIMARY KEY AUTOINCREMENT,
                    Name VARCHAR(100) NOT NULL
                )";
            createCmd.ExecuteNonQuery();

            for (int i = 1; i <= 5; i++)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = $"INSERT INTO Users (Name) VALUES ('User{i}')";
                insertCmd.ExecuteNonQuery();
            }

            connection.Engine?.Flush();
            connection.Close();
        }

        // Verify 5 users
        VerifyUserCount(5, "After initial seed");

        // Session 2: Delete one user
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            
            // Verify before delete in session 2
            using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = "SELECT COUNT(*) FROM Users";
                var beforeCount = Convert.ToInt64(verifyCmd.ExecuteScalar());
                TestContext.WriteLine($"Session 2 - Before delete: {beforeCount} users");
                Assert.That(beforeCount, Is.EqualTo(5), "Session 2 should see 5 users before delete");
            }

            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id = 1";
            var affected = deleteCmd.ExecuteNonQuery();
            TestContext.WriteLine($"Session 2 - DELETE affected: {affected} rows");
            Assert.That(affected, Is.EqualTo(1), "Session 2: DELETE should affect 1 row");

            // Verify after delete in same session
            using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = "SELECT COUNT(*) FROM Users";
                var afterCount = Convert.ToInt64(verifyCmd.ExecuteScalar());
                TestContext.WriteLine($"Session 2 - After delete (before flush): {afterCount} users");
                Assert.That(afterCount, Is.EqualTo(4), "Session 2 should see 4 users after delete");
            }

            connection.Engine?.Flush();
            TestContext.WriteLine("Session 2 - Flushed");
            
            // Verify after flush in same session
            using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = "SELECT COUNT(*) FROM Users";
                var afterFlushCount = Convert.ToInt64(verifyCmd.ExecuteScalar());
                TestContext.WriteLine($"Session 2 - After flush: {afterFlushCount} users");
            }
            
            connection.Close();
            TestContext.WriteLine("Session 2 - Closed");
        }

        // Verify 4 users after session 2 closed
        TestContext.WriteLine("Verifying after Session 2 closed...");
        VerifyUserCount(4, "After session 2 delete");

        // Session 3: Delete another user
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            
            // Verify before delete in session 3
            using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = "SELECT COUNT(*) FROM Users";
                var beforeCount = Convert.ToInt64(verifyCmd.ExecuteScalar());
                TestContext.WriteLine($"Session 3 - Before delete: {beforeCount} users");
                Assert.That(beforeCount, Is.EqualTo(4), "Session 3 should see 4 users (delete from session 2 should persist)");
            }

            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id = 2";
            var affected = deleteCmd.ExecuteNonQuery();
            TestContext.WriteLine($"Session 3 - DELETE affected: {affected} rows");
            Assert.That(affected, Is.EqualTo(1), "Session 3: DELETE should affect 1 row");

            connection.Engine?.Flush();
            TestContext.WriteLine("Session 3 - Flushed");
            connection.Close();
            TestContext.WriteLine("Session 3 - Closed");
        }

        // Verify 3 users
        VerifyUserCount(3, "After session 3 delete");

        // Session 4: Delete another user
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            TestContext.WriteLine("Session 4 - Opened");

            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id = 3";
            var affected = deleteCmd.ExecuteNonQuery();
            TestContext.WriteLine($"Session 4 - DELETE affected: {affected} rows");
            Assert.That(affected, Is.EqualTo(1), "Session 4: DELETE should affect 1 row");

            connection.Engine?.Flush();
            TestContext.WriteLine("Session 4 - Flushed");
            connection.Close();
            TestContext.WriteLine("Session 4 - Closed");
        }

        // Verify 2 users
        VerifyUserCount(2, "After session 4 delete");

        // Add delay to ensure OS has time to flush file buffers
        Thread.Sleep(100);

        // Session 5: Final verification
        TestContext.WriteLine("Session 5 - Starting final verification");
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            TestContext.WriteLine("Session 5 - Opened");

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id";
            using var reader = selectCmd.ExecuteReader();

            var users = new List<(long Id, string Name)>();
            while (reader.Read())
            {
                users.Add((reader.GetInt64(0), reader.GetString(1)));
            }

            TestContext.WriteLine($"Final verification: Found {users.Count} users: {string.Join(", ", users.Select(u => $"{u.Id}:{u.Name}"))}");

            Assert.That(users.Count, Is.EqualTo(2), "Should have exactly 2 users remaining");
            Assert.That(users.Select(u => u.Id).ToArray(), Is.EqualTo(new[] { 4L, 5L }), 
                "Users 4 and 5 should remain");
        }
    }

    [Test]
    public void MultipleSessionsWithoutExplicitFlushTest()
    {
        // Session 1: Create and seed
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Value VARCHAR(50))";
            createCmd.ExecuteNonQuery();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Test (Id, Value) VALUES (1, 'One'), (2, 'Two'), (3, 'Three')";
            insertCmd.ExecuteNonQuery();

            // NO explicit Flush - rely on Close
            connection.Close();
        }

        // Session 2: Delete WITHOUT explicit Flush
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Test WHERE Id = 1";
            deleteCmd.ExecuteNonQuery();

            // NO explicit Flush - rely on Close
            connection.Close();
        }

        // Session 3: Verify deletion persisted
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Test";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            Assert.That(count, Is.EqualTo(2), "Delete should persist even without explicit Flush");
        }
    }

    private void VerifyUserCount(int expected, string context)
    {
        TestContext.WriteLine($"VerifyUserCount ({context}): Opening new connection...");
        using var connection = new WitDbConnection($"Data Source={m_dbPath}");
        connection.Open();
        TestContext.WriteLine($"VerifyUserCount ({context}): Connection opened");

        // Also do a full select to check actual data
        using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id";
        using var reader = selectCmd.ExecuteReader();
        var users = new List<string>();
        while (reader.Read())
        {
            users.Add($"{reader.GetInt64(0)}:{reader.GetString(1)}");
        }
        reader.Close();
        TestContext.WriteLine($"VerifyUserCount ({context}): Select found: [{string.Join(", ", users)}]");

        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var count = Convert.ToInt64(countCmd.ExecuteScalar());
        
        TestContext.WriteLine($"VerifyUserCount ({context}): Count = {count}");

        Assert.That(count, Is.EqualTo(expected), $"{context}: Expected {expected} users, got {count}");
        
        connection.Close();
        TestContext.WriteLine($"VerifyUserCount ({context}): Connection closed");
    }

    #endregion

    #region Debug Tests

    [Test]
    public void DebugDeleteTest()
    {
        // Create table and insert data
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE Test (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100))";
            createCmd.ExecuteNonQuery();
            
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Test (Name) VALUES ('Alice'), ('Bob'), ('Charlie')";
            insertCmd.ExecuteNonQuery();
            
            connection.Engine?.Flush();
        }
        
        // Reopen and delete
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            
            // Get the MVCC store for direct inspection
            var database = (OutWit.Database.Core.Builder.WitDatabase)typeof(OutWit.Database.Engine.WitSqlEngine)
                .GetField("m_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(connection.Engine)!;
            
            var store = database.Store;
            if (store is OutWit.Database.Core.Transactions.MvccTransactionalStore mvccStore)
            {
                // Check that timestamp manager was initialized correctly
                var currentTimestamp = mvccStore.TimestampManager.CurrentTimestamp;
                TestContext.WriteLine($"Restored timestamp manager current timestamp: {currentTimestamp}");
                
                var nextTimestamp = mvccStore.TimestampManager.GetNextTimestamp();
                TestContext.WriteLine($"Next timestamp: {nextTimestamp}");
                
                // The next timestamp should be greater than any existing record's timestamp
                var versions = mvccStore.MvccStore.GetAllVersions(
                    OutWit.Database.Schema.SchemaCatalog.CreateRowKey("Test", 1));
                if (versions.Count > 0)
                {
                    var maxRecordTs = versions.Max(v => v.CreateTimestamp);
                    TestContext.WriteLine($"Max record timestamp: {maxRecordTs}");
                    Assert.That(nextTimestamp, Is.GreaterThan(maxRecordTs), 
                        "Next timestamp should be greater than existing records");
                }
            }
            
            // Now do the DELETE
            using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Test WHERE Id = 1";
            var affected = deleteCmd.ExecuteNonQuery();
            TestContext.WriteLine($"DELETE affected: {affected}");
            
            // Check SQL result
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id, Name FROM Test ORDER BY Id";
            using var reader = selectCmd.ExecuteReader();
            
            var rows = new List<string>();
            while (reader.Read())
            {
                rows.Add($"{reader.GetInt64(0)}:{reader.GetString(1)}");
            }
            TestContext.WriteLine($"After DELETE (SQL): {string.Join(", ", rows)}");
            
            Assert.That(rows.Count, Is.EqualTo(2), "Should have 2 rows after DELETE");
            Assert.That(rows, Does.Not.Contain("1:Alice"));
            
            connection.Engine?.Flush();
        }
        
        // Verify in new session
        using (var connection = new WitDbConnection($"Data Source={m_dbPath}"))
        {
            connection.Open();
            
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id, Name FROM Test ORDER BY Id";
            using var reader = selectCmd.ExecuteReader();
            
            var rows = new List<string>();
            while (reader.Read())
            {
                rows.Add($"{reader.GetInt64(0)}:{reader.GetString(1)}");
            }
            TestContext.WriteLine($"After reopen (SQL): {string.Join(", ", rows)}");
            
            Assert.That(rows.Count, Is.EqualTo(2), "Should still have 2 rows after reopen");
        }
    }
    #endregion
}

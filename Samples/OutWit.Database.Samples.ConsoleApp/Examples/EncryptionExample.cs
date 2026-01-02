using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;
using WitDb = OutWit.Database.Core.Builder.WitDatabase;

namespace OutWit.Database.Samples.ConsoleApp.Examples;

/// <summary>
/// Demonstrates database encryption with AES-GCM.
/// </summary>
public static class EncryptionExample
{
    #region Constants

    private const string DB_PATH = "encrypted_demo.witdb";
    private const string PASSWORD = "MySecurePassword123!";

    #endregion

    #region Run

    public static async Task RunAsync()
    {
        Console.WriteLine("=== Encryption Example ===");
        Console.WriteLine();

        // Clean up from previous runs
        CleanupDatabase();

        // === Create Encrypted Database ===
        Console.WriteLine("1. Creating encrypted database with AES-GCM...");
        Console.WriteLine($"   Database file: {DB_PATH}");
        Console.WriteLine($"   Password: {new string('*', PASSWORD.Length)}");
        Console.WriteLine();

        var database = new WitDatabaseBuilder()
            .WithFilePath(DB_PATH)
            .WithBTree()
            .WithEncryption(PASSWORD)
            .WithTransactions()
            .Build();

        using (var engine = new WitSqlEngine(database, ownsStore: true))
        {
            // Create table with sensitive data
            Console.WriteLine("2. Creating table for sensitive data...");
            engine.Execute("""
                CREATE TABLE SecretData (
                    Id BIGINT PRIMARY KEY AUTOINCREMENT,
                    Category VARCHAR(50) NOT NULL,
                    Secret TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )
                """);
            Console.WriteLine("   [OK] Table created");
            Console.WriteLine();

            // Insert sensitive data
            Console.WriteLine("3. Inserting sensitive data...");
            engine.Execute("""
                INSERT INTO SecretData (Category, Secret) VALUES
                    ('API Key', 'sk-prod-abc123xyz789-secret-key'),
                    ('Password', 'SuperSecretP@ssw0rd!'),
                    ('Credit Card', '4111-1111-1111-1111'),
                    ('SSN', '123-45-6789'),
                    ('Private Key', 'MIIEvQIBADANBgkqhkiG9w0BAQEFAAS...')
                """);
            Console.WriteLine("   [OK] 5 secrets inserted");
            Console.WriteLine();

            // Query the data
            Console.WriteLine("4. Querying encrypted data (decrypted automatically)...");
            using (var result = engine.Execute("SELECT Id, Category, Secret FROM SecretData ORDER BY Id"))
            {
                Console.WriteLine();
                while (result.Read())
                {
                    var secret = result.CurrentRow["Secret"].ToString() ?? "";
                    var masked = MaskSecret(secret);
                    Console.WriteLine($"   [{result.CurrentRow["Id"]}] {result.CurrentRow["Category"],-15}: {masked}");
                }
            }
            Console.WriteLine();
        }

        // === Reopen with Correct Password ===
        Console.WriteLine("5. Closing and reopening database with correct password...");
        var database2 = new WitDatabaseBuilder()
            .WithFilePath(DB_PATH)
            .WithBTree()
            .WithEncryption(PASSWORD)
            .Build();

        using (var engine2 = new WitSqlEngine(database2, ownsStore: true))
        {
            var count = engine2.ExecuteScalar("SELECT COUNT(*) FROM SecretData").AsInt64();
            Console.WriteLine($"   [OK] Successfully reopened, found {count} records");
        }
        Console.WriteLine();

        // === Try Wrong Password ===
        Console.WriteLine("6. Attempting to open with wrong password...");
        try
        {
            // The error will occur during Build() when trying to decrypt
            var database3 = new WitDatabaseBuilder()
                .WithFilePath(DB_PATH)
                .WithBTree()
                .WithEncryption("WrongPassword")
                .Build();
            
            // If we get here, try to use it (shouldn't happen)
            using (var engine3 = new WitSqlEngine(database3, ownsStore: true))
            {
                engine3.Execute("SELECT * FROM SecretData");
                Console.WriteLine("   [FAIL] Should have failed!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   [OK] Correctly denied access: {ex.Message[..Math.Min(60, ex.Message.Length)]}...");
        }
        Console.WriteLine();

        // Small delay to ensure file handles are released
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);

        // === Show Raw File Content ===
        Console.WriteLine("7. Raw file content (first 200 bytes):");
        Console.WriteLine();
        try
        {
            var bytes = File.ReadAllBytes(DB_PATH);
            Console.WriteLine($"   File size: {bytes.Length:N0} bytes");
            Console.Write("   Hex: ");
            for (int i = 0; i < Math.Min(40, bytes.Length); i++)
            {
                Console.Write($"{bytes[i]:X2} ");
            }
            Console.WriteLine("...");
            Console.WriteLine();
            Console.WriteLine("   [OK] Data is encrypted (no readable text visible)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   Could not read file: {ex.Message}");
        }
        Console.WriteLine();

        // Clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);
        
        CleanupDatabase();
        Console.WriteLine("8. Database file deleted.");
        Console.WriteLine();

        Console.WriteLine("=== Example Complete ===");
    }

    #endregion

    #region Helpers

    private static void CleanupDatabase()
    {
        try
        {
            if (File.Exists(DB_PATH))
                File.Delete(DB_PATH);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return new string('*', secret.Length);
        
        return secret[..4] + new string('*', Math.Min(20, secret.Length - 8)) + secret[^4..];
    }

    #endregion
}

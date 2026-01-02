using OutWit.Database.AdoNet;

namespace OutWit.Database.Samples.WebApi.Services;

/// <summary>
/// Service for initializing database schema and seed data.
/// </summary>
public class DatabaseInitializer
{
    #region Constants

    private const string CREATE_USERS_TABLE = """
        CREATE TABLE IF NOT EXISTS Users (
            Id BIGINT PRIMARY KEY AUTOINCREMENT,
            Name VARCHAR(100) NOT NULL,
            Email VARCHAR(255) NOT NULL UNIQUE,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        )
        """;

    private const string CREATE_PRODUCTS_TABLE = """
        CREATE TABLE IF NOT EXISTS Products (
            Id BIGINT PRIMARY KEY AUTOINCREMENT,
            Name VARCHAR(100) NOT NULL,
            Description VARCHAR(500),
            Price DECIMAL(15, 2) NOT NULL,
            Stock INT NOT NULL DEFAULT 0,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        )
        """;

    private const string CREATE_EMAIL_INDEX = """
        CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email)
        """;

    private const string CREATE_PRODUCT_NAME_INDEX = """
        CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products (Name)
        """;

    #endregion

    #region Fields

    private readonly WitDbConnection m_connection;

    #endregion

    #region Constructors

    public DatabaseInitializer(WitDbConnection connection)
    {
        m_connection = connection;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the database schema and seeds data if empty.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create tables
        await ExecuteNonQueryAsync(CREATE_USERS_TABLE);
        await ExecuteNonQueryAsync(CREATE_PRODUCTS_TABLE);

        // Create indexes
        await ExecuteNonQueryAsync(CREATE_EMAIL_INDEX);
        await ExecuteNonQueryAsync(CREATE_PRODUCT_NAME_INDEX);

        // Seed data if tables are empty
        var userCount = await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users");
        if (userCount == 0)
        {
            await SeedUsersAsync();
        }

        var productCount = await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Products");
        if (productCount == 0)
        {
            await SeedProductsAsync();
        }

        // Persist schema and seed data to disk
        SaveChanges();
    }

    #endregion

    #region Seeding

    private async Task SeedUsersAsync()
    {
        var users = new[]
        {
            ("Alice Johnson", "alice@example.com"),
            ("Bob Smith", "bob@example.com"),
            ("Carol Williams", "carol@example.com")
        };

        const string sql = "INSERT INTO Users (Name, Email) VALUES (@name, @email)";

        foreach (var (name, email) in users)
        {
            using var command = m_connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new WitDbParameter("@name", name));
            command.Parameters.Add(new WitDbParameter("@email", email));
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedProductsAsync()
    {
        var products = new[]
        {
            ("Laptop", "High-performance laptop", 1299.99m, 50),
            ("Mouse", "Wireless mouse", 29.99m, 200),
            ("Keyboard", "Mechanical keyboard", 149.99m, 100),
            ("Monitor", "4K display", 499.99m, 30),
            ("Headphones", "Noise-cancelling headphones", 249.99m, 75)
        };

        const string sql = """
            INSERT INTO Products (Name, Description, Price, Stock) 
            VALUES (@name, @description, @price, @stock)
            """;

        foreach (var (name, description, price, stock) in products)
        {
            using var command = m_connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new WitDbParameter("@name", name));
            command.Parameters.Add(new WitDbParameter("@description", description));
            command.Parameters.Add(new WitDbParameter("@price", price));
            command.Parameters.Add(new WitDbParameter("@stock", stock));
            await command.ExecuteNonQueryAsync();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Persists all pending changes to disk.
    /// </summary>
    private void SaveChanges()
    {
        m_connection.Engine?.Flush();
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T?> ExecuteScalarAsync<T>(string sql)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    #endregion
}

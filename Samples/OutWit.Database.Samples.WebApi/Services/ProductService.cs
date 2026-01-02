using OutWit.Database.AdoNet;
using OutWit.Database.Samples.WebApi.Models;

namespace OutWit.Database.Samples.WebApi.Services;

/// <summary>
/// Service for product operations using ADO.NET.
/// </summary>
public class ProductService
{
    #region Fields

    private readonly WitDbConnection m_connection;

    #endregion

    #region Constructors

    public ProductService(WitDbConnection connection)
    {
        m_connection = connection;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all products.
    /// </summary>
    public async Task<List<Product>> GetAllAsync()
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, Price, Stock, CreatedAt FROM Products ORDER BY Name";

        var products = new List<Product>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    public async Task<Product?> GetByIdAsync(long id)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, Price, Stock, CreatedAt FROM Products WHERE Id = @id";
        command.Parameters.Add(new WitDbParameter("@id", id));

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadProduct(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    public async Task<Product> CreateAsync(string name, string description, decimal price, int stock)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products (Name, Description, Price, Stock) 
            VALUES (@name, @description, @price, @stock)
            RETURNING Id, Name, Description, Price, Stock, CreatedAt
            """;
        command.Parameters.Add(new WitDbParameter("@name", name));
        command.Parameters.Add(new WitDbParameter("@description", description));
        command.Parameters.Add(new WitDbParameter("@price", price));
        command.Parameters.Add(new WitDbParameter("@stock", stock));

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var product = ReadProduct(reader);
            SaveChanges();
            return product;
        }

        throw new InvalidOperationException("Failed to create product");
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    public async Task<Product?> UpdateAsync(long id, string name, string description, decimal price, int stock)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            UPDATE Products 
            SET Name = @name, Description = @description, Price = @price, Stock = @stock
            WHERE Id = @id
            RETURNING Id, Name, Description, Price, Stock, CreatedAt
            """;
        command.Parameters.Add(new WitDbParameter("@id", id));
        command.Parameters.Add(new WitDbParameter("@name", name));
        command.Parameters.Add(new WitDbParameter("@description", description));
        command.Parameters.Add(new WitDbParameter("@price", price));
        command.Parameters.Add(new WitDbParameter("@stock", stock));

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var product = ReadProduct(reader);
            SaveChanges();
            return product;
        }

        return null;
    }

    /// <summary>
    /// Updates product stock.
    /// </summary>
    public async Task<Product?> UpdateStockAsync(long id, int quantity)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            UPDATE Products 
            SET Stock = Stock + @quantity
            WHERE Id = @id
            RETURNING Id, Name, Description, Price, Stock, CreatedAt
            """;
        command.Parameters.Add(new WitDbParameter("@id", id));
        command.Parameters.Add(new WitDbParameter("@quantity", quantity));

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var product = ReadProduct(reader);
            SaveChanges();
            return product;
        }

        return null;
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    public async Task<bool> DeleteAsync(long id)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = @id";
        command.Parameters.Add(new WitDbParameter("@id", id));

        var affected = await command.ExecuteNonQueryAsync();
        
        if (affected > 0)
        {
            SaveChanges();
        }
        
        return affected > 0;
    }

    #endregion

    #region Queries

    /// <summary>
    /// Searches products by name.
    /// </summary>
    public async Task<List<Product>> SearchByNameAsync(string searchTerm)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Description, Price, Stock, CreatedAt 
            FROM Products 
            WHERE LOWER(Name) LIKE @search OR LOWER(Description) LIKE @search
            ORDER BY Name
            """;
        command.Parameters.Add(new WitDbParameter("@search", $"%{searchTerm.ToLowerInvariant()}%"));

        var products = new List<Product>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    /// <summary>
    /// Gets products with low stock.
    /// </summary>
    public async Task<List<Product>> GetLowStockAsync(int threshold = 10)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Description, Price, Stock, CreatedAt 
            FROM Products 
            WHERE Stock < @threshold
            ORDER BY Stock ASC
            """;
        command.Parameters.Add(new WitDbParameter("@threshold", threshold));

        var products = new List<Product>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    /// <summary>
    /// Gets products by price range.
    /// </summary>
    public async Task<List<Product>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Description, Price, Stock, CreatedAt 
            FROM Products 
            WHERE Price >= @minPrice AND Price <= @maxPrice
            ORDER BY Price
            """;
        command.Parameters.Add(new WitDbParameter("@minPrice", minPrice));
        command.Parameters.Add(new WitDbParameter("@maxPrice", maxPrice));

        var products = new List<Product>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    /// <summary>
    /// Gets paginated products.
    /// </summary>
    public async Task<(List<Product> Products, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        // Get total count
        using var countCommand = m_connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Products";
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        // Get paginated results
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Description, Price, Stock, CreatedAt 
            FROM Products 
            ORDER BY Name
            LIMIT @limit OFFSET @offset
            """;
        command.Parameters.Add(new WitDbParameter("@limit", pageSize));
        command.Parameters.Add(new WitDbParameter("@offset", (page - 1) * pageSize));

        var products = new List<Product>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            products.Add(ReadProduct(reader));
        }

        return (products, totalCount);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets product statistics.
    /// </summary>
    public async Task<ProductStatistics> GetStatisticsAsync()
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT 
                COUNT(*) AS TotalProducts,
                SUM(Stock) AS TotalStock,
                AVG(Price) AS AveragePrice,
                MIN(Price) AS MinPrice,
                MAX(Price) AS MaxPrice
            FROM Products
            """;

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProductStatistics
            {
                TotalProducts = reader.GetInt32(0),
                TotalStock = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                AveragePrice = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                MinPrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                MaxPrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
            };
        }

        return new ProductStatistics();
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk inserts products in a transaction.
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<(string Name, string Description, decimal Price, int Stock)> products)
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        try
        {
            int count = 0;
            const string sql = """
                INSERT INTO Products (Name, Description, Price, Stock) 
                VALUES (@name, @description, @price, @stock)
                """;

            foreach (var (name, description, price, stock) in products)
            {
                using var command = m_connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.Add(new WitDbParameter("@name", name));
                command.Parameters.Add(new WitDbParameter("@description", description));
                command.Parameters.Add(new WitDbParameter("@price", price));
                command.Parameters.Add(new WitDbParameter("@stock", stock));
                await command.ExecuteNonQueryAsync();
                count++;
            }

            transaction.Commit();
            SaveChanges(); // Persist after commit
            return count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Persists all pending changes to disk.
    /// Similar to DbContext.SaveChanges() in EF Core.
    /// </summary>
    private void SaveChanges()
    {
        m_connection.Engine?.Flush();
    }

    private static Product ReadProduct(System.Data.Common.DbDataReader reader)
    {
        return new Product
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Price = reader.GetDecimal(3),
            Stock = reader.GetInt32(4),
            CreatedAt = reader.GetDateTime(5)
        };
    }

    #endregion
}

using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Models;

namespace OutWit.Database.Samples.WebApiEF.Services;

/// <summary>
/// Service for product operations.
/// </summary>
public sealed class ProductService
{
    #region Fields

    private readonly AppDbContext m_context;

    #endregion

    #region Constructors

    public ProductService(AppDbContext context)
    {
        m_context = context;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all products.
    /// </summary>
    public async Task<List<Product>> GetAllAsync()
    {
        return await m_context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    public async Task<Product?> GetByIdAsync(int id)
    {
        return await m_context.Products.FindAsync(id);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    public async Task<Product> CreateAsync(string name, string? description, decimal price, int stock)
    {
        var product = new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Stock = stock
        };

        m_context.Products.Add(product);
        await m_context.SaveChangesAsync();
        return product;
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    public async Task<Product?> UpdateAsync(int id, string name, string? description, decimal price, int stock)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return null;

        product.Name = name;
        product.Description = description;
        product.Price = price;
        product.Stock = stock;

        await m_context.SaveChangesAsync();
        return product;
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return false;

        m_context.Products.Remove(product);
        await m_context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Searches products by name.
    /// </summary>
    public async Task<List<Product>> SearchByNameAsync(string searchTerm)
    {
        return await m_context.Products
            .Where(p => p.Name.Contains(searchTerm))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets paginated products.
    /// </summary>
    public async Task<(List<Product> Products, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        var totalCount = await m_context.Products.CountAsync();

        var products = await m_context.Products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (products, totalCount);
    }

    /// <summary>
    /// Gets products by price range.
    /// </summary>
    public async Task<List<Product>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice)
    {
        return await m_context.Products
            .Where(p => p.Price >= minPrice && p.Price <= maxPrice)
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    /// <summary>
    /// Gets products in stock.
    /// </summary>
    public async Task<List<Product>> GetInStockAsync()
    {
        return await m_context.Products
            .Where(p => p.Stock > 0)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets product statistics.
    /// </summary>
    public async Task<ProductStatistics> GetStatisticsAsync()
    {
        var products = await m_context.Products.ToListAsync();

        if (products.Count == 0)
        {
            return new ProductStatistics();
        }

        return new ProductStatistics
        {
            TotalProducts = products.Count,
            TotalStock = products.Sum(p => p.Stock),
            AveragePrice = products.Average(p => p.Price),
            MinPrice = products.Min(p => p.Price),
            MaxPrice = products.Max(p => p.Price)
        };
    }

    #endregion
}

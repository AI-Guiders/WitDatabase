using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;

namespace OutWit.Database.Samples.WebApiEF.Controllers;

/// <summary>
/// API controller for product operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    #region Fields

    private readonly AppDbContext m_context;

    #endregion

    #region Constructors

    public ProductsController(AppDbContext context)
    {
        m_context = context;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Gets all products.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll()
    {
        var products = await m_context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(long id)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        return new ProductDto(product);
    }

    /// <summary>
    /// Searches products by name.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<ProductDto>>> Search([FromQuery] string q)
    {
        var products = await m_context.Products
            .Where(p => p.Name.Contains(q))
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock
        };

        m_context.Products.Add(product);
        await m_context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new ProductDto(product));
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(long id, [FromBody] UpdateProductRequest request)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;

        await m_context.SaveChangesAsync();
        return new ProductDto(product);
    }

    /// <summary>
    /// Updates product stock.
    /// </summary>
    [HttpPatch("{id}/stock")]
    public async Task<ActionResult<ProductDto>> UpdateStock(long id, [FromBody] UpdateStockRequest request)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        product.Stock = request.Stock;
        await m_context.SaveChangesAsync();

        return new ProductDto(product);
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(long id)
    {
        var product = await m_context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        m_context.Products.Remove(product);
        await m_context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Gets low stock products.
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<List<ProductDto>>> GetLowStock([FromQuery] int threshold = 10)
    {
        var products = await m_context.Products
            .Where(p => p.Stock < threshold)
            .OrderBy(p => p.Stock)
            .ToListAsync();

        return products.Select(p => new ProductDto(p)).ToList();
    }

    #endregion
}

#region DTOs

/// <summary>
/// Product data transfer object.
/// </summary>
public class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }

    public ProductDto() { }

    public ProductDto(Product product)
    {
        Id = product.Id;
        Name = product.Name;
        Description = product.Description;
        Price = product.Price;
        Stock = product.Stock;
    }
}

/// <summary>
/// Create product request.
/// </summary>
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Update product request.
/// </summary>
public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Update stock request.
/// </summary>
public class UpdateStockRequest
{
    public int Stock { get; set; }
}

#endregion

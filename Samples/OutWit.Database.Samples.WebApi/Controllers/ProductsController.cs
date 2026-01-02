using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApi.Models;
using OutWit.Database.Samples.WebApi.Services;

namespace OutWit.Database.Samples.WebApi.Controllers;

/// <summary>
/// API controller for product operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    #region Fields

    private readonly ProductService m_productService;

    #endregion

    #region Constructors

    public ProductsController(ProductService productService)
    {
        m_productService = productService;
    }

    #endregion

    #region CRUD Endpoints

    /// <summary>
    /// Gets all products.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll()
    {
        var products = await m_productService.GetAllAsync();
        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(long id)
    {
        var product = await m_productService.GetByIdAsync(id);
        if (product == null)
            return NotFound();

        return new ProductDto(product);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        if (request.Price < 0)
            return BadRequest("Price cannot be negative");
        if (request.Stock < 0)
            return BadRequest("Stock cannot be negative");

        var product = await m_productService.CreateAsync(
            request.Name, request.Description, request.Price, request.Stock);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new ProductDto(product));
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(long id, [FromBody] UpdateProductRequest request)
    {
        if (request.Price < 0)
            return BadRequest("Price cannot be negative");
        if (request.Stock < 0)
            return BadRequest("Stock cannot be negative");

        var product = await m_productService.UpdateAsync(
            id, request.Name, request.Description, request.Price, request.Stock);
        if (product == null)
            return NotFound();

        return new ProductDto(product);
    }

    /// <summary>
    /// Updates product stock.
    /// </summary>
    [HttpPatch("{id}/stock")]
    public async Task<ActionResult<ProductDto>> UpdateStock(long id, [FromBody] UpdateStockRequest request)
    {
        var product = await m_productService.UpdateStockAsync(id, request.Quantity);
        if (product == null)
            return NotFound();

        if (product.Stock < 0)
            return BadRequest("Resulting stock cannot be negative");

        return new ProductDto(product);
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await m_productService.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Searches products by name or description.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<ProductDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search term is required");

        var products = await m_productService.SearchByNameAsync(q);
        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets products with low stock.
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<List<ProductDto>>> GetLowStock([FromQuery] int threshold = 10)
    {
        var products = await m_productService.GetLowStockAsync(threshold);
        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets products by price range.
    /// </summary>
    [HttpGet("price-range")]
    public async Task<ActionResult<List<ProductDto>>> GetByPriceRange(
        [FromQuery] decimal minPrice = 0, 
        [FromQuery] decimal maxPrice = decimal.MaxValue)
    {
        if (minPrice < 0)
            return BadRequest("minPrice cannot be negative");
        if (maxPrice < minPrice)
            return BadRequest("maxPrice must be greater than or equal to minPrice");

        var products = await m_productService.GetByPriceRangeAsync(minPrice, maxPrice);
        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets paginated products.
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (products, totalCount) = await m_productService.GetPagedAsync(page, pageSize);

        return new PagedResult<ProductDto>
        {
            Items = products.Select(p => new ProductDto(p)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    #endregion

    #region Statistics Endpoints

    /// <summary>
    /// Gets product statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<ProductStatistics>> GetStatistics()
    {
        return await m_productService.GetStatisticsAsync();
    }

    #endregion

    #region Bulk Endpoints

    /// <summary>
    /// Bulk imports products.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<BulkImportResult>> BulkImport([FromBody] List<CreateProductRequest> products)
    {
        if (products == null || products.Count == 0)
            return BadRequest("Products list cannot be empty");

        var data = products.Select(p => (p.Name, p.Description, p.Price, p.Stock));
        var count = await m_productService.BulkInsertAsync(data);

        return new BulkImportResult
        {
            ImportedCount = count,
            Message = $"Successfully imported {count} products"
        };
    }

    #endregion
}

/// <summary>
/// Bulk import result.
/// </summary>
public class BulkImportResult
{
    public int ImportedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

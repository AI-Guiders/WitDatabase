using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApiEF.Models;
using OutWit.Database.Samples.WebApiEF.Services;

namespace OutWit.Database.Samples.WebApiEF.Controllers;

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
    public async Task<ActionResult<ProductDto>> GetById(int id)
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
        var product = await m_productService.CreateAsync(
            request.Name, 
            request.Description, 
            request.Price, 
            request.Stock);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new ProductDto(product));
    }

    /// <summary>
    /// Updates a product.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var product = await m_productService.UpdateAsync(
            id, 
            request.Name, 
            request.Description, 
            request.Price, 
            request.Stock);

        if (product == null)
            return NotFound();

        return new ProductDto(product);
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var deleted = await m_productService.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Searches products by name.
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

    /// <summary>
    /// Gets products by price range.
    /// </summary>
    [HttpGet("price-range")]
    public async Task<ActionResult<List<ProductDto>>> GetByPriceRange(
        [FromQuery] decimal min = 0, 
        [FromQuery] decimal max = decimal.MaxValue)
    {
        var products = await m_productService.GetByPriceRangeAsync(min, max);
        return products.Select(p => new ProductDto(p)).ToList();
    }

    /// <summary>
    /// Gets products in stock.
    /// </summary>
    [HttpGet("in-stock")]
    public async Task<ActionResult<List<ProductDto>>> GetInStock()
    {
        var products = await m_productService.GetInStockAsync();
        return products.Select(p => new ProductDto(p)).ToList();
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
}

namespace OutWit.Database.Samples.WebApi.Models;

/// <summary>
/// Product entity.
/// </summary>
public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Product data transfer object.
/// </summary>
public class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }

    public ProductDto() { }

    public ProductDto(Product product)
    {
        Id = product.Id;
        Name = product.Name;
        Description = product.Description;
        Price = product.Price;
        Stock = product.Stock;
        CreatedAt = product.CreatedAt;
    }
}

/// <summary>
/// Create product request.
/// </summary>
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Update product request.
/// </summary>
public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Update stock request.
/// </summary>
public class UpdateStockRequest
{
    public int Quantity { get; set; }
}

/// <summary>
/// Product statistics.
/// </summary>
public class ProductStatistics
{
    public int TotalProducts { get; set; }
    public int TotalStock { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

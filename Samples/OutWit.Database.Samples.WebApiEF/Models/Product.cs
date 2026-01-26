namespace OutWit.Database.Samples.WebApiEF.Models;

/// <summary>
/// Product entity.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Product data transfer object.
/// </summary>
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }

    #region Constructors

    public ProductDto() { }

    public ProductDto(Product product)
    {
        Id = product.Id;
        Name = product.Name;
        Description = product.Description;
        Price = product.Price;
        Stock = product.Stock;
    }

    #endregion
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

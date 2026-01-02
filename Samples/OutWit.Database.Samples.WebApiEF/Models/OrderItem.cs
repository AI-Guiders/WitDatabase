namespace OutWit.Database.Samples.WebApiEF.Models;

/// <summary>
/// Order item entity.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Order item data transfer object.
/// </summary>
public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    #region Constructors

    public OrderItemDto() { }

    public OrderItemDto(OrderItem item)
    {
        Id = item.Id;
        ProductId = item.ProductId;
        Quantity = item.Quantity;
        UnitPrice = item.UnitPrice;
    }

    #endregion
}

namespace OutWit.Database.Samples.WebApiEF.Models;

/// <summary>
/// Order status enumeration.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// Order entity.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
}

/// <summary>
/// Order data transfer object.
/// </summary>
public class OrderDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();

    #region Constructors

    public OrderDto() { }

    public OrderDto(Order order)
    {
        Id = order.Id;
        UserId = order.UserId;
        TotalAmount = order.TotalAmount;
        OrderDate = order.OrderDate;
        Status = order.Status;
    }

    #endregion
}

/// <summary>
/// Create order request.
/// </summary>
public class CreateOrderRequest
{
    public int UserId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

/// <summary>
/// Create order item request.
/// </summary>
public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Update order status request.
/// </summary>
public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}

/// <summary>
/// Order statistics.
/// </summary>
public class OrderStatistics
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public Dictionary<OrderStatus, int> OrdersByStatus { get; set; } = new();
}

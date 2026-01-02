using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Services;

namespace OutWit.Database.Samples.WebApiEF.Controllers;

/// <summary>
/// API controller for order operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    #region Fields

    private readonly OrderService m_orderService;

    #endregion

    #region Constructors

    public OrdersController(OrderService orderService)
    {
        m_orderService = orderService;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Gets all orders with pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (orders, totalCount) = await m_orderService.GetAllAsync(page, pageSize);
        
        return new PagedResult<OrderDto>
        {
            Items = orders.Select(o => new OrderDto(o)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    /// <summary>
    /// Gets orders for a specific user.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<OrderDto>>> GetByUser(long userId)
    {
        var orders = await m_orderService.GetByUserIdAsync(userId);
        return orders.Select(o => new OrderDto(o)).ToList();
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(long id)
    {
        var order = await m_orderService.GetByIdAsync(id);
        if (order == null)
            return NotFound();

        return new OrderDto(order);
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request)
    {
        try
        {
            var order = await m_orderService.CreateAsync(request.UserId, request.Items);
            if (order == null)
                return BadRequest("User not found");

            // Reload with includes
            order = await m_orderService.GetByIdAsync(order.Id);
            return CreatedAtAction(nameof(GetById), new { id = order!.Id }, new OrderDto(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Updates order status.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(long id, [FromBody] UpdateStatusRequest request)
    {
        var order = await m_orderService.UpdateStatusAsync(id, request.Status);
        if (order == null)
            return NotFound();

        // Reload with includes
        order = await m_orderService.GetByIdAsync(order.Id);
        return new OrderDto(order!);
    }

    /// <summary>
    /// Cancels an order.
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(long id)
    {
        var cancelled = await m_orderService.CancelAsync(id);
        if (!cancelled)
            return BadRequest("Order cannot be cancelled");

        return NoContent();
    }

    /// <summary>
    /// Gets order statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<OrderStatistics>> GetStatistics()
    {
        return await m_orderService.GetStatisticsAsync();
    }

    /// <summary>
    /// Gets top selling products.
    /// </summary>
    [HttpGet("top-products")]
    public async Task<ActionResult<List<ProductSales>>> GetTopProducts([FromQuery] int count = 5)
    {
        return await m_orderService.GetTopProductsAsync(count);
    }

    #endregion
}

#region DTOs

/// <summary>
/// Order data transfer object.
/// </summary>
public class OrderDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();

    public OrderDto() { }

    public OrderDto(Order order)
    {
        Id = order.Id;
        UserId = order.UserId;
        UserName = order.User?.Name ?? string.Empty;
        TotalAmount = order.TotalAmount;
        OrderDate = order.OrderDate;
        Status = order.Status.ToString();
        Items = order.Items?.Select(i => new OrderItemDto(i)).ToList() ?? new();
    }
}

/// <summary>
/// Order item data transfer object.
/// </summary>
public class OrderItemDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    public OrderItemDto() { }

    public OrderItemDto(OrderItem item)
    {
        Id = item.Id;
        ProductId = item.ProductId;
        ProductName = item.Product?.Name ?? string.Empty;
        Quantity = item.Quantity;
        UnitPrice = item.UnitPrice;
    }
}

/// <summary>
/// Create order request.
/// </summary>
public class CreateOrderRequest
{
    public long UserId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

/// <summary>
/// Update status request.
/// </summary>
public class UpdateStatusRequest
{
    public OrderStatus Status { get; set; }
}

/// <summary>
/// Paged result wrapper.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

#endregion

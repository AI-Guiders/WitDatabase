using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApiEF.Models;
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

    #region CRUD Endpoints

    /// <summary>
    /// Gets all orders.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAll()
    {
        var orders = await m_orderService.GetAllAsync();
        return orders.Select(o => new OrderDto(o)).ToList();
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var order = await m_orderService.GetByIdAsync(id);
        if (order == null)
            return NotFound();

        var dto = new OrderDto(order);
        var items = await m_orderService.GetOrderItemsAsync(id);
        dto.Items = items.Select(i => new OrderItemDto(i)).ToList();

        return dto;
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
            return BadRequest("Order must have at least one item");

        var items = request.Items.Select(i => (i.ProductId, i.Quantity)).ToList();
        var order = await m_orderService.CreateAsync(request.UserId, items);

        if (order == null)
            return BadRequest("Invalid user or product, or insufficient stock");

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new OrderDto(order));
    }

    /// <summary>
    /// Updates order status.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await m_orderService.UpdateStatusAsync(id, request.Status);
        if (order == null)
            return NotFound();

        return new OrderDto(order);
    }

    /// <summary>
    /// Deletes an order.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var deleted = await m_orderService.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Gets orders by user ID.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<OrderDto>>> GetByUserId(int userId)
    {
        var orders = await m_orderService.GetByUserIdAsync(userId);
        return orders.Select(o => new OrderDto(o)).ToList();
    }

    /// <summary>
    /// Gets orders by status.
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<OrderDto>>> GetByStatus(OrderStatus status)
    {
        var orders = await m_orderService.GetByStatusAsync(status);
        return orders.Select(o => new OrderDto(o)).ToList();
    }

    /// <summary>
    /// Gets paginated orders.
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (orders, totalCount) = await m_orderService.GetPagedAsync(page, pageSize);

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(o => new OrderDto(o)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    #endregion

    #region Statistics Endpoints

    /// <summary>
    /// Gets order statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<OrderStatistics>> GetStatistics()
    {
        return await m_orderService.GetStatisticsAsync();
    }

    #endregion
}

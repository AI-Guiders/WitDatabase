using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;

namespace OutWit.Database.Samples.WebApiEF.Services;

/// <summary>
/// Service for order operations.
/// </summary>
public class OrderService
{
    #region Fields

    private readonly AppDbContext m_context;

    #endregion

    #region Constructors

    public OrderService(AppDbContext context)
    {
        m_context = context;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all orders with pagination.
    /// </summary>
    public async Task<(List<Order> Orders, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 10)
    {
        var query = m_context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate);

        var totalCount = await query.CountAsync();
        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (orders, totalCount);
    }

    /// <summary>
    /// Gets orders for a specific user.
    /// </summary>
    public async Task<List<Order>> GetByUserIdAsync(long userId)
    {
        return await m_context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    public async Task<Order?> GetByIdAsync(long id)
    {
        return await m_context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    public async Task<Order?> CreateAsync(long userId, List<OrderItemRequest> items)
    {
        // Validate user exists
        var user = await m_context.Users.FindAsync(userId);
        if (user == null)
            return null;

        // Validate products and create order items
        var orderItems = new List<OrderItem>();
        decimal totalAmount = 0;

        foreach (var item in items)
        {
            var product = await m_context.Products.FindAsync(item.ProductId);
            if (product == null)
                throw new InvalidOperationException($"Product {item.ProductId} not found");

            if (product.Stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for product {product.Name}");

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            };

            orderItems.Add(orderItem);
            totalAmount += orderItem.Quantity * orderItem.UnitPrice;

            // Update stock
            product.Stock -= item.Quantity;
        }

        var order = new Order
        {
            UserId = userId,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            Items = orderItems
        };

        m_context.Orders.Add(order);
        await m_context.SaveChangesAsync();

        return order;
    }

    /// <summary>
    /// Updates order status.
    /// </summary>
    public async Task<Order?> UpdateStatusAsync(long id, OrderStatus status)
    {
        var order = await m_context.Orders.FindAsync(id);
        if (order == null)
            return null;

        order.Status = status;
        await m_context.SaveChangesAsync();

        return order;
    }

    /// <summary>
    /// Cancels an order and restores stock.
    /// </summary>
    public async Task<bool> CancelAsync(long id)
    {
        var order = await m_context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return false;

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            return false;

        // Restore stock
        foreach (var item in order.Items)
        {
            var product = await m_context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.Stock += item.Quantity;
            }
        }

        order.Status = OrderStatus.Cancelled;
        await m_context.SaveChangesAsync();

        return true;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets order statistics.
    /// </summary>
    public async Task<OrderStatistics> GetStatisticsAsync()
    {
        var totalOrders = await m_context.Orders.CountAsync();
        var totalRevenue = await m_context.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => o.TotalAmount);

        var ordersByStatus = await m_context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count);

        var averageOrderValue = totalOrders > 0 
            ? await m_context.Orders.AverageAsync(o => o.TotalAmount) 
            : 0;

        return new OrderStatistics
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            AverageOrderValue = averageOrderValue,
            OrdersByStatus = ordersByStatus
        };
    }

    /// <summary>
    /// Gets top selling products.
    /// </summary>
    public async Task<List<ProductSales>> GetTopProductsAsync(int count = 5)
    {
        return await m_context.OrderItems
            .GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new ProductSales
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                TotalQuantity = g.Sum(i => i.Quantity),
                TotalRevenue = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(count)
            .ToListAsync();
    }

    #endregion
}

/// <summary>
/// Order item request DTO.
/// </summary>
public class OrderItemRequest
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Order statistics DTO.
/// </summary>
public class OrderStatistics
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();
}

/// <summary>
/// Product sales DTO.
/// </summary>
public class ProductSales
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

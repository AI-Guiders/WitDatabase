using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Models;

namespace OutWit.Database.Samples.WebApiEF.Services;

/// <summary>
/// Service for order operations.
/// </summary>
public sealed class OrderService
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
    /// Gets all orders.
    /// </summary>
    public async Task<List<Order>> GetAllAsync()
    {
        return await m_context.Orders
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    public async Task<Order?> GetByIdAsync(int id)
    {
        return await m_context.Orders.FindAsync(id);
    }

    /// <summary>
    /// Gets order items for an order.
    /// </summary>
    public async Task<List<OrderItem>> GetOrderItemsAsync(int orderId)
    {
        return await m_context.OrderItems
            .Where(i => i.OrderId == orderId)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    public async Task<Order?> CreateAsync(int userId, List<(int ProductId, int Quantity)> items)
    {
        // Verify user exists
        var user = await m_context.Users.FindAsync(userId);
        if (user == null)
            return null;

        // Verify all products exist and have sufficient stock
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await m_context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != productIds.Count)
            return null;

        foreach (var (productId, quantity) in items)
        {
            if (products[productId].Stock < quantity)
                return null;
        }

        // Create order
        var order = new Order
        {
            UserId = userId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = 0
        };

        m_context.Orders.Add(order);
        await m_context.SaveChangesAsync();

        // Create order items and calculate total
        decimal totalAmount = 0;
        foreach (var (productId, quantity) in items)
        {
            var product = products[productId];
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            };

            totalAmount += product.Price * quantity;
            product.Stock -= quantity;

            m_context.OrderItems.Add(orderItem);
        }

        order.TotalAmount = totalAmount;
        await m_context.SaveChangesAsync();

        return order;
    }

    /// <summary>
    /// Updates order status.
    /// </summary>
    public async Task<Order?> UpdateStatusAsync(int id, OrderStatus status)
    {
        var order = await m_context.Orders.FindAsync(id);
        if (order == null)
            return null;

        order.Status = status;
        await m_context.SaveChangesAsync();

        return order;
    }

    /// <summary>
    /// Deletes an order.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var order = await m_context.Orders.FindAsync(id);
        if (order == null)
            return false;

        // Get order items and restore product stock
        var orderItems = await m_context.OrderItems
            .Where(i => i.OrderId == id)
            .ToListAsync();

        foreach (var item in orderItems)
        {
            var product = await m_context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.Stock += item.Quantity;
            }
            m_context.OrderItems.Remove(item);
        }

        m_context.Orders.Remove(order);
        await m_context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Gets orders by user ID.
    /// </summary>
    public async Task<List<Order>> GetByUserIdAsync(int userId)
    {
        return await m_context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets orders by status.
    /// </summary>
    public async Task<List<Order>> GetByStatusAsync(OrderStatus status)
    {
        return await m_context.Orders
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets paginated orders.
    /// </summary>
    public async Task<(List<Order> Orders, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        var totalCount = await m_context.Orders.CountAsync();

        var orders = await m_context.Orders
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (orders, totalCount);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets order statistics.
    /// </summary>
    public async Task<OrderStatistics> GetStatisticsAsync()
    {
        var orders = await m_context.Orders.ToListAsync();

        var stats = new OrderStatistics
        {
            TotalOrders = orders.Count,
            TotalRevenue = orders.Sum(o => o.TotalAmount),
            AverageOrderValue = orders.Count > 0 ? orders.Average(o => o.TotalAmount) : 0
        };

        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            stats.OrdersByStatus[status] = orders.Count(o => o.Status == status);
        }

        return stats;
    }

    #endregion
}

namespace OutWit.Database.EntityFramework.Benchmarks;

/// <summary>
/// User entity for benchmarks.
/// </summary>
public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>
/// Order entity for benchmarks.
/// </summary>
public class Order
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "pending";

    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

/// <summary>
/// OrderItem entity for benchmarks.
/// </summary>
public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

/// <summary>
/// Product entity for benchmarks.
/// </summary>
public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
}

using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

// Configure WitDatabase with Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=webapi_ef_sample.witdb";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseWitDb(connectionString));

// Register services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();

// Add controllers
builder.Services.AddControllers();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WitDatabase WebAPI EF Sample v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    
    // Seed data if empty
    if (!await context.Users.AnyAsync())
    {
        await SeedDataAsync(context);
    }
}

app.Run();

// Seed initial data
static async Task SeedDataAsync(AppDbContext context)
{
    var users = new[]
    {
        new User { Name = "Alice Johnson", Email = "alice@example.com" },
        new User { Name = "Bob Smith", Email = "bob@example.com" },
        new User { Name = "Carol Williams", Email = "carol@example.com" }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    var products = new[]
    {
        new Product { Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, Stock = 50 },
        new Product { Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, Stock = 200 },
        new Product { Name = "Keyboard", Description = "Mechanical keyboard", Price = 149.99m, Stock = 100 },
        new Product { Name = "Monitor", Description = "4K display", Price = 499.99m, Stock = 30 }
    };

    context.Products.AddRange(products);
    await context.SaveChangesAsync();

    // Create some orders
    var order1 = new Order
    {
        UserId = users[0].Id,
        Status = OrderStatus.Completed,
        Items = new List<OrderItem>
        {
            new() { ProductId = products[0].Id, Quantity = 1, UnitPrice = products[0].Price },
            new() { ProductId = products[1].Id, Quantity = 2, UnitPrice = products[1].Price }
        }
    };
    order1.TotalAmount = order1.Items.Sum(i => i.Quantity * i.UnitPrice);

    var order2 = new Order
    {
        UserId = users[1].Id,
        Status = OrderStatus.Pending,
        Items = new List<OrderItem>
        {
            new() { ProductId = products[2].Id, Quantity = 1, UnitPrice = products[2].Price }
        }
    };
    order2.TotalAmount = order2.Items.Sum(i => i.Quantity * i.UnitPrice);

    context.Orders.AddRange(order1, order2);
    await context.SaveChangesAsync();
}

// Make models accessible for seeding
public partial class Program { }

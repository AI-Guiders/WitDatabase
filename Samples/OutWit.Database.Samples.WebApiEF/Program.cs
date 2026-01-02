using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure WitDatabase with Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=webapi_ef_sample.witdb";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseWitDb(connectionString));

// Register services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();

// Add controllers
builder.Services.AddControllers();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure Swagger
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

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await InitializeDatabaseAsync(context);
}

Console.WriteLine("WebApiEF sample started. Use Swagger UI to interact with the API.");

app.Run();

/// <summary>
/// Initializes database tables using raw SQL.
/// </summary>
static async Task InitializeDatabaseAsync(AppDbContext context)
{
    await context.Database.OpenConnectionAsync();
    var connection = context.Database.GetDbConnection();
    
    var statements = new[]
    {
        @"CREATE TABLE IF NOT EXISTS ""User"" (""Id"" INT PRIMARY KEY AUTOINCREMENT, ""Name"" TEXT NOT NULL, ""Email"" TEXT NOT NULL, ""CreatedAt"" DATETIME)",
        @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_User_Email"" ON ""User"" (""Email"")",
        @"CREATE TABLE IF NOT EXISTS ""Product"" (""Id"" INT PRIMARY KEY AUTOINCREMENT, ""Name"" TEXT NOT NULL, ""Description"" TEXT, ""Price"" DECIMAL NOT NULL, ""Stock"" INT NOT NULL)",
        @"CREATE INDEX IF NOT EXISTS ""IX_Product_Name"" ON ""Product"" (""Name"")",
        @"CREATE TABLE IF NOT EXISTS ""Order"" (""Id"" INT PRIMARY KEY AUTOINCREMENT, ""UserId"" INT NOT NULL, ""TotalAmount"" DECIMAL NOT NULL, ""OrderDate"" DATETIME, ""Status"" INT NOT NULL)",
        @"CREATE INDEX IF NOT EXISTS ""IX_Order_UserId"" ON ""Order"" (""UserId"")",
        @"CREATE INDEX IF NOT EXISTS ""IX_Order_OrderDate"" ON ""Order"" (""OrderDate"")",
        @"CREATE TABLE IF NOT EXISTS ""OrderItem"" (""Id"" INT PRIMARY KEY AUTOINCREMENT, ""OrderId"" INT NOT NULL, ""ProductId"" INT NOT NULL, ""Quantity"" INT NOT NULL, ""UnitPrice"" DECIMAL NOT NULL)"
    };

    foreach (var sql in statements)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
    
    await context.Database.CloseConnectionAsync();
}

public partial class Program { }

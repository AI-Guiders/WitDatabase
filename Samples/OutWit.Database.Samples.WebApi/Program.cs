using OutWit.Database.AdoNet;
using OutWit.Database.Samples.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure connection string for WitDatabase
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=webapi_sample.witdb";

// Create and open the shared connection
var sharedConnection = new WitDbConnection(connectionString);
sharedConnection.Open();

// Register as singleton - will be disposed when app shuts down
builder.Services.AddSingleton(sharedConnection);

// Register services
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProductService>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WitDatabase WebAPI Sample v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Initialize database schema and seed data
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Run the app - when stopped, properly dispose connection
try
{
    await app.RunAsync();
}
finally
{
    // Ensure data is flushed and connection is properly closed
    Console.WriteLine("Shutting down - flushing database...");
    sharedConnection.Engine?.Flush();
    sharedConnection.Close();
    sharedConnection.Dispose();
    Console.WriteLine("Database closed.");
}

// Make Program accessible for testing
public partial class Program { }

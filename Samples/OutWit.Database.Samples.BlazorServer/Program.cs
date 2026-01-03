using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using OutWit.Database.EntityFramework.Extensions;
using OutWit.Database.Samples.BlazorServer.Components;
using OutWit.Database.Samples.BlazorServer.Data;
using OutWit.Database.Samples.BlazorServer.Models;
using OutWit.Database.Samples.BlazorServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure WitDatabase with Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=identity_server.witdb";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseWitDb(connectionString);
});

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Register application services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthenticationService>();

// Register database initializer
builder.Services.AddHostedService<DatabaseInitializerService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("==============================================");
Console.WriteLine("  WitDatabase Identity Server Sample");
Console.WriteLine("==============================================");
Console.WriteLine();
Console.WriteLine("Default admin credentials:");
Console.WriteLine("  Email:    admin@example.com");
Console.WriteLine("  Password: Admin123!");
Console.WriteLine();
Console.WriteLine("==============================================");

app.Run();

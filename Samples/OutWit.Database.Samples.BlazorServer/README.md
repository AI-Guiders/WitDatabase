# WitDatabase Blazor Server Identity Sample

A complete ASP.NET Core Identity server built with Blazor Server, WitDatabase, and MudBlazor.

## Features

- **ASP.NET Core Identity** - User authentication and authorization
- **WitDatabase + Entity Framework Core** - Embedded database storage
- **Role-based authorization** - Administrator and User roles
- **User management** - CRUD operations for users
- **MudBlazor UI** - Material Design components
- **Schema initialization** - Automatic database setup via HostedService

## Quick Start

### Running the Sample

```bash
cd Samples/OutWit.Database.Samples.BlazorServer
dotnet run
```

Open http://localhost:5200 in your browser.

### Default Credentials

| Field | Value |
|-------|-------|
| Email | admin@example.com |
| Password | Admin123! |

## Architecture

```
???????????????????????????????????????????????????
?                   UI Layer                      ?
?  (Blazor Server + MudBlazor Components)         ?
???????????????????????????????????????????????????
?              Service Layer                       ?
?  (AuthenticationService, UserService)           ?
???????????????????????????????????????????????????
?            ASP.NET Core Identity                 ?
?  (UserManager, SignInManager, RoleManager)      ?
???????????????????????????????????????????????????
?           Entity Framework Core                  ?
?  (ApplicationDbContext)                         ?
???????????????????????????????????????????????????
?              WitDatabase                         ?
?  (Embedded Database Storage)                    ?
???????????????????????????????????????????????????
```

## Project Structure

```
OutWit.Database.Samples.BlazorServer/
??? Components/
?   ??? Layout/
?   ?   ??? MainLayout.razor      # Main app layout
?   ?   ??? EmptyLayout.razor     # Login page layout
?   ??? Pages/
?   ?   ??? Home.razor            # Dashboard
?   ?   ??? Login.razor           # Login page
?   ?   ??? Logout.razor          # Logout handler
?   ?   ??? Users.razor           # User management
?   ?   ??? About.razor           # About page
?   ??? App.razor                 # HTML shell
?   ??? Routes.razor              # Router config
?   ??? RedirectToLogin.razor     # Auth redirect
?   ??? _Imports.razor            # Global usings
??? Data/
?   ??? ApplicationDbContext.cs   # EF Core context
??? Models/
?   ??? ApplicationUser.cs        # Extended Identity user
?   ??? ApplicationRole.cs        # Extended Identity role
??? Services/
?   ??? AuthenticationService.cs  # Login/logout logic
?   ??? UserService.cs            # User CRUD
?   ??? DatabaseInitializerService.cs # Schema + seed
??? Program.cs                    # App configuration
??? appsettings.json
??? Properties/
    ??? launchSettings.json
```

## Key Features

### Extended User Model

```csharp
public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}
```

### Database Initialization

The `DatabaseInitializerService` runs at startup and:
1. Creates all required Identity tables
2. Seeds default roles (Administrator, User)
3. Creates admin user if not exists

### Role-Based Authorization

Pages use `[Authorize]` attribute for protection:

```razor
@page "/users"
@attribute [Authorize(Roles = "Administrator")]
```

### Password Requirements

- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character

## Configuration

### Connection String

Configure in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=identity_server.witdb"
  }
}
```

### Identity Options

Configured in `Program.cs`:

```csharp
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
});
```

## Technologies

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 |
| UI | Blazor Server + MudBlazor |
| Authentication | ASP.NET Core Identity |
| ORM | Entity Framework Core |
| Database | WitDatabase |

## License

MIT License - See LICENSE file for details.

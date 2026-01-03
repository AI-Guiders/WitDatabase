using Microsoft.AspNetCore.Identity;
using OutWit.Database.Samples.BlazorServer.Models;

namespace OutWit.Database.Samples.BlazorServer.Services;

/// <summary>
/// Service for handling user authentication.
/// </summary>
public class AuthenticationService
{
    #region Fields

    private readonly SignInManager<ApplicationUser> m_signInManager;
    private readonly UserManager<ApplicationUser> m_userManager;
    private readonly UserService m_userService;
    private readonly ILogger<AuthenticationService> m_logger;

    #endregion

    #region Constructors

    public AuthenticationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        UserService userService,
        ILogger<AuthenticationService> logger)
    {
        m_signInManager = signInManager;
        m_userManager = userManager;
        m_userService = userService;
        m_logger = logger;
    }

    #endregion

    #region Authentication

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await m_userManager.FindByEmailAsync(email);

        if (user == null)
        {
            m_logger.LogWarning("Login failed: User not found for email {Email}", email);
            return (false, "Invalid email or password");
        }

        if (!user.IsActive)
        {
            m_logger.LogWarning("Login failed: User {Email} is inactive", email);
            return (false, "Your account has been deactivated");
        }

        var result = await m_signInManager.PasswordSignInAsync(
            user,
            password,
            rememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await m_userService.UpdateLastLoginAsync(user);
            m_logger.LogInformation("User {Email} logged in successfully", email);
            return (true, null);
        }

        if (result.IsLockedOut)
        {
            m_logger.LogWarning("User {Email} is locked out", email);
            return (false, "Your account has been locked. Please try again later.");
        }

        m_logger.LogWarning("Login failed for user {Email}", email);
        return (false, "Invalid email or password");
    }

    public async Task LogoutAsync()
    {
        await m_signInManager.SignOutAsync();
        m_logger.LogInformation("User logged out");
    }

    public async Task<ApplicationUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        return await m_userManager.GetUserAsync(principal);
    }

    public bool IsAuthenticated(System.Security.Claims.ClaimsPrincipal principal)
    {
        return principal.Identity?.IsAuthenticated ?? false;
    }

    #endregion
}

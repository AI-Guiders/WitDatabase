using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;

namespace OutWit.Database.Samples.WebApiEF.Services;

/// <summary>
/// Service for user operations.
/// </summary>
public class UserService
{
    #region Fields

    private readonly AppDbContext m_context;

    #endregion

    #region Constructors

    public UserService(AppDbContext context)
    {
        m_context = context;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all users.
    /// </summary>
    public async Task<List<User>> GetAllAsync()
    {
        return await m_context.Users
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public async Task<User?> GetByIdAsync(long id)
    {
        return await m_context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await m_context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<User> CreateAsync(string name, string email)
    {
        var user = new User
        {
            Name = name,
            Email = email
        };

        m_context.Users.Add(user);
        await m_context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    public async Task<User?> UpdateAsync(long id, string name, string email)
    {
        var user = await m_context.Users.FindAsync(id);
        if (user == null)
            return null;

        user.Name = name;
        user.Email = email;

        await m_context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    public async Task<bool> DeleteAsync(long id)
    {
        var user = await m_context.Users.FindAsync(id);
        if (user == null)
            return false;

        m_context.Users.Remove(user);
        await m_context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets user statistics.
    /// </summary>
    public async Task<UserStatistics> GetStatisticsAsync()
    {
        var totalUsers = await m_context.Users.CountAsync();
        var usersWithOrders = await m_context.Users
            .Where(u => u.Orders.Any())
            .CountAsync();

        var topSpender = await m_context.Users
            .Select(u => new 
            { 
                User = u, 
                TotalSpent = u.Orders.Sum(o => o.TotalAmount) 
            })
            .OrderByDescending(x => x.TotalSpent)
            .FirstOrDefaultAsync();

        return new UserStatistics
        {
            TotalUsers = totalUsers,
            UsersWithOrders = usersWithOrders,
            TopSpenderName = topSpender?.User.Name,
            TopSpenderAmount = topSpender?.TotalSpent ?? 0
        };
    }

    #endregion
}

/// <summary>
/// User statistics DTO.
/// </summary>
public class UserStatistics
{
    public int TotalUsers { get; set; }
    public int UsersWithOrders { get; set; }
    public string? TopSpenderName { get; set; }
    public decimal TopSpenderAmount { get; set; }
}

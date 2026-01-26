using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Models;

namespace OutWit.Database.Samples.WebApiEF.Services;

/// <summary>
/// Service for user operations.
/// </summary>
public sealed class UserService
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
    public async Task<User?> GetByIdAsync(int id)
    {
        return await m_context.Users.FindAsync(id);
    }

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await m_context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<User> CreateAsync(string name, string email)
    {
        var user = new User
        {
            Name = name,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        m_context.Users.Add(user);
        await m_context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Updates a user.
    /// </summary>
    public async Task<User?> UpdateAsync(int id, string name, string email)
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
    public async Task<bool> DeleteAsync(int id)
    {
        var user = await m_context.Users.FindAsync(id);
        if (user == null)
            return false;

        m_context.Users.Remove(user);
        await m_context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Searches users by name.
    /// </summary>
    public async Task<List<User>> SearchByNameAsync(string searchTerm)
    {
        return await m_context.Users
            .Where(u => u.Name.Contains(searchTerm))
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets paginated users.
    /// </summary>
    public async Task<(List<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        var totalCount = await m_context.Users.CountAsync();

        var users = await m_context.Users
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets user statistics.
    /// </summary>
    public async Task<UserStatistics> GetStatisticsAsync()
    {
        var users = await m_context.Users.ToListAsync();

        return new UserStatistics
        {
            TotalUsers = users.Count,
            NewestUser = users.MaxBy(u => u.CreatedAt)?.CreatedAt,
            OldestUser = users.MinBy(u => u.CreatedAt)?.CreatedAt
        };
    }

    #endregion
}

using OutWit.Database.AdoNet;
using OutWit.Database.Samples.WebApi.Models;

namespace OutWit.Database.Samples.WebApi.Services;

/// <summary>
/// Service for user operations using ADO.NET.
/// </summary>
public class UserService
{
    #region Fields

    private readonly WitDbConnection m_connection;
    private readonly ILogger<UserService>? m_logger;

    #endregion

    #region Constructors

    public UserService(WitDbConnection connection, ILogger<UserService>? logger = null)
    {
        m_connection = connection;
        m_logger = logger;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets all users.
    /// </summary>
    public Task<List<User>> GetAllAsync()
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Email, CreatedAt FROM Users ORDER BY Name";

        var users = new List<User>();
        using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return Task.FromResult(users);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public Task<User?> GetByIdAsync(long id)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Email, CreatedAt FROM Users WHERE Id = @id";
        command.Parameters.Add(new WitDbParameter("@id", id));

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return Task.FromResult<User?>(ReadUser(reader));
        }

        return Task.FromResult<User?>(null);
    }

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    public Task<User?> GetByEmailAsync(string email)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Email, CreatedAt FROM Users WHERE Email = @email";
        command.Parameters.Add(new WitDbParameter("@email", email));

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return Task.FromResult<User?>(ReadUser(reader));
        }

        return Task.FromResult<User?>(null);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public Task<User> CreateAsync(string name, string email)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Name, Email) 
            VALUES (@name, @email)
            RETURNING Id, Name, Email, CreatedAt
            """;
        command.Parameters.Add(new WitDbParameter("@name", name));
        command.Parameters.Add(new WitDbParameter("@email", email));

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var user = ReadUser(reader);
            SaveChanges(); // Persist to disk
            return Task.FromResult(user);
        }

        throw new InvalidOperationException("Failed to create user");
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    public Task<User?> UpdateAsync(long id, string name, string email)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            UPDATE Users 
            SET Name = @name, Email = @email 
            WHERE Id = @id
            RETURNING Id, Name, Email, CreatedAt
            """;
        command.Parameters.Add(new WitDbParameter("@id", id));
        command.Parameters.Add(new WitDbParameter("@name", name));
        command.Parameters.Add(new WitDbParameter("@email", email));

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var user = ReadUser(reader);
            SaveChanges(); // Persist to disk
            return Task.FromResult<User?>(user);
        }

        return Task.FromResult<User?>(null);
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    public Task<bool> DeleteAsync(long id)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = @id";
        command.Parameters.Add(new WitDbParameter("@id", id));

        var affected = command.ExecuteNonQuery();
        
        if (affected > 0)
        {
            SaveChanges(); // Persist to disk
        }
        
        return Task.FromResult(affected > 0);
    }

    #endregion

    #region Queries

    /// <summary>
    /// Searches users by name.
    /// </summary>
    public Task<List<User>> SearchByNameAsync(string searchTerm)
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Email, CreatedAt 
            FROM Users 
            WHERE LOWER(Name) LIKE @search
            ORDER BY Name
            """;
        command.Parameters.Add(new WitDbParameter("@search", $"%{searchTerm.ToLowerInvariant()}%"));

        var users = new List<User>();
        using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return Task.FromResult(users);
    }

    /// <summary>
    /// Gets paginated users.
    /// </summary>
    public Task<(List<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        // Get total count
        using var countCommand = m_connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Users";
        var totalCount = Convert.ToInt32(countCommand.ExecuteScalar());

        // Get paginated results
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Email, CreatedAt 
            FROM Users 
            ORDER BY Name
            LIMIT @limit OFFSET @offset
            """;
        command.Parameters.Add(new WitDbParameter("@limit", pageSize));
        command.Parameters.Add(new WitDbParameter("@offset", (page - 1) * pageSize));

        var users = new List<User>();
        using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return Task.FromResult((users, totalCount));
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets user statistics.
    /// </summary>
    public Task<UserStatistics> GetStatisticsAsync()
    {
        using var command = m_connection.CreateCommand();
        command.CommandText = """
            SELECT 
                COUNT(*) AS TotalUsers,
                MIN(CreatedAt) AS OldestUser,
                MAX(CreatedAt) AS NewestUser
            FROM Users
            """;

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return Task.FromResult(new UserStatistics
            {
                TotalUsers = reader.GetInt32(0),
                OldestUser = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                NewestUser = reader.IsDBNull(2) ? null : reader.GetDateTime(2)
            });
        }

        return Task.FromResult(new UserStatistics());
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Persists all pending changes to disk.
    /// Similar to DbContext.SaveChanges() in EF Core.
    /// </summary>
    private void SaveChanges()
    {
        m_connection.Engine?.Flush();
    }

    private static User ReadUser(System.Data.Common.DbDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Email = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3)
        };
    }

    #endregion
}

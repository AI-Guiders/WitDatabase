namespace OutWit.Database.Samples.WebApiEF.Models;

/// <summary>
/// User entity.
/// </summary>
public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// User data transfer object.
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    #region Constructors

    public UserDto() { }

    public UserDto(User user)
    {
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        CreatedAt = user.CreatedAt;
    }

    #endregion
}

/// <summary>
/// Create user request.
/// </summary>
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Update user request.
/// </summary>
public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// User statistics.
/// </summary>
public class UserStatistics
{
    public int TotalUsers { get; set; }
    public DateTime? NewestUser { get; set; }
    public DateTime? OldestUser { get; set; }
}

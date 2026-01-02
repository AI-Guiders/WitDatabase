namespace OutWit.Database.Samples.WebApi.Models;

/// <summary>
/// User entity.
/// </summary>
public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// User data transfer object.
/// </summary>
public class UserDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public UserDto() { }

    public UserDto(User user)
    {
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        CreatedAt = user.CreatedAt;
    }
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

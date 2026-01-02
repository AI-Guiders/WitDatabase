using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApiEF.Data;
using OutWit.Database.Samples.WebApiEF.Services;

namespace OutWit.Database.Samples.WebApiEF.Controllers;

/// <summary>
/// API controller for user operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    #region Fields

    private readonly UserService m_userService;

    #endregion

    #region Constructors

    public UsersController(UserService userService)
    {
        m_userService = userService;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Gets all users.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await m_userService.GetAllAsync();
        return users.Select(u => new UserDto(u)).ToList();
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(long id)
    {
        var user = await m_userService.GetByIdAsync(id);
        if (user == null)
            return NotFound();

        return new UserDto(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        // Check if email already exists
        var existing = await m_userService.GetByEmailAsync(request.Email);
        if (existing != null)
            return BadRequest("Email already in use");

        var user = await m_userService.CreateAsync(request.Name, request.Email);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new UserDto(user));
    }

    /// <summary>
    /// Updates a user.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(long id, [FromBody] UpdateUserRequest request)
    {
        var user = await m_userService.UpdateAsync(id, request.Name, request.Email);
        if (user == null)
            return NotFound();

        return new UserDto(user);
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(long id)
    {
        var deleted = await m_userService.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Gets user statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<UserStatistics>> GetStatistics()
    {
        return await m_userService.GetStatisticsAsync();
    }

    #endregion
}

#region DTOs

/// <summary>
/// User data transfer object.
/// </summary>
public class UserDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }

    public UserDto() { }

    public UserDto(User user)
    {
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        CreatedAt = user.CreatedAt;
        OrderCount = user.Orders?.Count ?? 0;
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

#endregion

using Microsoft.AspNetCore.Mvc;
using OutWit.Database.Samples.WebApi.Models;
using OutWit.Database.Samples.WebApi.Services;

namespace OutWit.Database.Samples.WebApi.Controllers;

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

    #region CRUD Endpoints

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

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Searches users by name.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<UserDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search term is required");

        var users = await m_userService.SearchByNameAsync(q);
        return users.Select(u => new UserDto(u)).ToList();
    }

    /// <summary>
    /// Gets paginated users.
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (users, totalCount) = await m_userService.GetPagedAsync(page, pageSize);

        return new PagedResult<UserDto>
        {
            Items = users.Select(u => new UserDto(u)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    #endregion

    #region Statistics Endpoints

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

/// <summary>
/// Generic paged result.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

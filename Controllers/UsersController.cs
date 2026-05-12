using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int perPage = 15)
    {
        var total = await _db.Users.CountAsync();
        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new PaginatedResult<UserDto>
        {
            Data = users,
            CurrentPage = page,
            LastPage = (int)Math.Ceiling((double)total / perPage),
            PerPage = perPage,
            Total = total
        });
    }

    [HttpPost]
    public async Task<IActionResult> Store([FromBody] CreateUserRequest request)
    {
        if (request.Password != request.PasswordConfirmation)
            return BadRequest(new { message = "Password dan konfirmasi tidak cocok." });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email sudah digunakan." });

        var user = new Models.User
        {
            Name = request.Name,
            Email = request.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "User berhasil ditambahkan." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (id == currentUserId)
            return BadRequest(new { message = "Tidak bisa menghapus akun sendiri." });

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "User berhasil dihapus." });
    }
}

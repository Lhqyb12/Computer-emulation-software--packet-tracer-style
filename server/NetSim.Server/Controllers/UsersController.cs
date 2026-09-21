using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Dtos;

namespace NetSim.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll()
    {
        var users = await _db.Users
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                Role = u.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return NotFound();

        string? callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (target.Id.ToString() == callerId)
            return BadRequest("Cannot delete your own account.");

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

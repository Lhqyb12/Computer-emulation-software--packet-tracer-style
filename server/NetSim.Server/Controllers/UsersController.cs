using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Dtos;
using NetSim.Server.Services;


namespace NetSim.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SecurityLog _security;

    public UsersController(AppDbContext db, SecurityLog security)
    {
        _db = db;
        _security = security;
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
        
        // A deleted account leaves no trace in the Users table - this row is the only record of who
        // deleted whom, and when. The admin's email comes from their token, not from anything they typed
        string? adminEmail = User.FindFirstValue(ClaimTypes.Email);
        await _security.RecordAsync("User deleted", target.Email, $"Deleted by admin {adminEmail}");


        return NoContent();
    }
}

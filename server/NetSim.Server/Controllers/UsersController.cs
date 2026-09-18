using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Dtos;
using NetSim.Server.Models;

namespace NetSim.Server.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/users - מחזיר את כל המשתמשים, רק אם הקורא הוא Admin
    [HttpGet]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll([FromHeader(Name = "X-User-Email")] string? callerEmail)
    {
        var caller = await GetCallerAsync(callerEmail);
        if (caller is null)
            return Unauthorized("Missing or unknown caller identity.");
        if (caller.Role != "Admin")
            return StatusCode(403);

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

    // DELETE /api/users/5 - מוחק משתמש לפי Id, רק אם הקורא הוא Admin
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, [FromHeader(Name = "X-User-Email")] string? callerEmail)
    {
        var caller = await GetCallerAsync(callerEmail);
        if (caller is null)
            return Unauthorized("Missing or unknown caller identity.");
        if (caller.Role != "Admin")
            return StatusCode(403);

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return NotFound();

        // בטיחות: מנהל לא יכול למחוק בטעות את עצמו ולהינעל מחוץ למערכת
        if (target.Id == caller.Id)
            return BadRequest("Cannot delete your own account.");

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // מתודת עזר משותפת: קוראת את ה-Header "X-User-Email" שהלקוח שולח, ומאתרת את המשתמש המתאים במסד.
    // זהו "מנגנון הזיהוי הפשוט והזמני" שסיכמנו עליו - הלקוח פשוט "אומר" לשרת מי הוא בכל בקשה,
    // בלי טוקן אמיתי. השרת סומך על ההצהרה הזו (זו בדיוק הסיבה שזה זמני ולא מאובטח באמת -
    // בעתיד אפשר להחליף את זה ב-JWT בלי לגעת בכלל בשני ה-Actions למעלה)
    private async Task<User?> GetCallerAsync(string? callerEmail)
    {
        if (string.IsNullOrWhiteSpace(callerEmail))
            return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == callerEmail);
    }
}

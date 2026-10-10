using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Dtos;

namespace NetSim.Server.Services;

// The admin actions on user accounts. This logic used to sit inside UsersController;
// it lives here now so the socket server can call it without any HTTP around it
public class UserService
{
    private readonly AppDbContext _db;
    private readonly SecurityLog _security;

    public UserService(AppDbContext db, SecurityLog security)
    {
        _db = db;
        _security = security;
    }

    public async Task<List<UserSummaryDto>> GetAllAsync()
    {
        return await _db.Users
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
    }

    // callerId and adminEmail come from the admin's verified token, never from anything they typed
    public async Task<AuthResponse> DeleteAsync(int id, string? callerId, string? adminEmail)
    {
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return new AuthResponse { Success = false, Message = "User not found." };

        if (target.Id.ToString() == callerId)
            return new AuthResponse { Success = false, Message = "Cannot delete your own account." };

        _db.Users.Remove(target);
        await _db.SaveChangesAsync();

        // A deleted account leaves no trace in the Users table - this row is the only record of who
        // deleted whom, and when
        await _security.RecordAsync("User deleted", target.Email, $"Deleted by admin {adminEmail}");

        return new AuthResponse { Success = true, Message = "User deleted." };
    }
}

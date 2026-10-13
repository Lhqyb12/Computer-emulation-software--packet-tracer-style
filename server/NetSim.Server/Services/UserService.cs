using Microsoft.EntityFrameworkCore;  // FirstOrDefaultAsync / ToListAsync - asynchronous DB queries
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // UserSummaryDto, AuthResponse

namespace NetSim.Server.Services;

// The admin actions on user accounts. This logic used to sit inside UsersController;
// it lives here now so the socket server can call it without any HTTP around it
public class UserService
{
    // The connection to the database
    private readonly AppDbContext _db;

    // Records security events (here: who deleted whom)
    private readonly SecurityLog _security;

    // The constructor. Dependency injection passes in the two objects
    public UserService(AppDbContext db, SecurityLog security)
    {
        _db = db;
        _security = security;
    }

    // Returns all the users, as the rows of the admin's "Users" screen
    public async Task<List<UserSummaryDto>> GetAllAsync()
    {
        // Select = for every user in the table, build a UserSummaryDto with only these six fields.
        // EF Core turns it into a SQL SELECT of only these columns, so PasswordHash never even
        // leaves the database
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
            // Run the query and collect the results into a list
            .ToListAsync();
    }

    // Deletes one user. id = the user to delete.
    // callerId and adminEmail come from the admin's verified token, never from anything they typed
    public async Task<AuthResponse> DeleteAsync(int id, string? callerId, string? adminEmail)
    {
        // Find the user to delete. null = no user has this id
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (target is null)
            return new AuthResponse { Success = false, Message = "User not found." };

        // An admin must not delete their own account (the system could be left with no admin at all).
        // The id in the token is text, so the number is turned into text for the comparison
        if (target.Id.ToString() == callerId)
            return new AuthResponse { Success = false, Message = "Cannot delete your own account." };

        // Mark the row for deletion...
        _db.Users.Remove(target);

        // ...and run the DELETE statement
        await _db.SaveChangesAsync();

        // A deleted account leaves no trace in the Users table - this row is the only record of who
        // deleted whom, and when
        await _security.RecordAsync("User deleted", target.Email, $"Deleted by admin {adminEmail}");

        // The user was deleted
        return new AuthResponse { Success = true, Message = "User deleted." };
    }
}

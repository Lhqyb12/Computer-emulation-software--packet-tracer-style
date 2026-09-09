using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Dtos;
using NetSim.Server.Models;

namespace NetSim.Server.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        string? passwordError = PasswordPolicy.Validate(request.Password, request.Username);
        if (passwordError is not null)
            return new AuthResponse { Success = false, Message = passwordError };

        bool emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
            return new AuthResponse { Success = false, Message = "Email is already registered." };

        string hash = PasswordHasher.Hash(request.Password);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = hash,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AuthResponse { Success = true, Message = "Account created." };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null)
            return new AuthResponse { Success = false, Message = "Wrong email or password." };

        bool ok = PasswordHasher.Verify(request.Password, user.PasswordHash);
        if (!ok)
            return new AuthResponse { Success = false, Message = "Wrong email or password." };

        return new AuthResponse { Success = true, Message = "Signed in." };
    }
}

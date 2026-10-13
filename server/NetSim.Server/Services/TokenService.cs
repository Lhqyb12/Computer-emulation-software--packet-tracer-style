using System.Security.Claims;                  // Claim, ClaimsIdentity, ClaimTypes - the details stored inside a token
using System.Text;                             // Encoding.UTF8: text -> bytes
using Microsoft.Extensions.Configuration;      // IConfiguration - the server's settings
using Microsoft.IdentityModel.JsonWebTokens;   // JsonWebTokenHandler - creates and checks JWTs
using Microsoft.IdentityModel.Tokens;          // SymmetricSecurityKey, SigningCredentials, TokenValidationParameters
using NetSim.Server.Models;                    // User

namespace NetSim.Server.Services;

// Creates and checks JWTs (JSON Web Tokens). A token is a short signed text the client receives at login
// and sends back with every later request, so it does not have to send the password again.
// The token says who the user is; our signature on it proves that we wrote it and nobody changed it
public class TokenService
{
    // The server's settings; the secret signing key ("Jwt:Key") is read from here
    private readonly IConfiguration _config;

    // The constructor. Dependency injection passes in the settings
    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    // Builds a signed token for a user who just logged in
    public string CreateToken(User user)
    {
        // The secret key, turned from text into bytes. "Symmetric" = the same key both signs and checks.
        // "!" = we promise the setting exists (Program.cs refuses to start without it)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        // The description of the token: what is written in it, when it expires, and how it is signed
        var descriptor = new SecurityTokenDescriptor
        {
            // Subject = the "claims": the facts the token states about the user
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),  // the user's id in our database
                new Claim(ClaimTypes.Email, user.Email),                   // the user's email
                new Claim(ClaimTypes.Role, user.Role),                     // "User" or "Admin"
            }),

            // The token stops being valid one hour from now; after that the user logs in again
            Expires = DateTime.UtcNow.AddHours(1),

            // Sign with our key using HMAC-SHA256. Without the key nobody can produce a matching signature
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        // Build the token text ("header.payload.signature") and return it
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Checks a token a client sent. Returns the identity written inside it (id, email, role),
    // or null if the token is missing, forged, damaged or expired
    public async Task<ClaimsIdentity?> ValidateTokenAsync(string token)
    {
        // No token at all - nothing to check
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // The same secret key the token was signed with in CreateToken
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        // Check the token against these rules
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            // The signature must match our key - this is what makes a forged or edited token fail
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            // We do not write an "issuer" or an "audience" into our tokens, so there is nothing to check for them
            ValidateIssuer = false,
            ValidateAudience = false,

            // An expired token is refused, with no extra minutes of grace
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        });

        // Valid -> the identity that was written in the token; not valid -> null
        return result.IsValid ? result.ClaimsIdentity : null;
    }

}

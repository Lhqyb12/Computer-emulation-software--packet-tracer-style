using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;



namespace NetSim.App.Services;

// הצורה של ה-JSON שהשרת מחזיר
public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // True when the server refused a login only because the email was never verified
    public bool NeedsVerification { get; set; }



}

// שורה אחת בטבלת ניהול המשתמשים - תואם ל-UserSummaryDto בצד השרת
public class UserSummaryDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string Role { get; set; } = string.Empty;
}

// The answer to "get-users" - matches UsersResponse on the server
public class UsersResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<UserSummaryDto> Users { get; set; } = new();
}



// מדבר עם השרת NetSim.Server דרך HTTP
public class AuthApiClient
{
    

    public Task<AuthResult> RegisterAsync(string username, string email, string password) =>
        SendAsync("register", new { username, email, password });



    public Task<AuthResult> LoginAsync(string email, string password) =>
        SendAsync("login", new { email, password });


    public Task<AuthResult> ForgotPasswordAsync(string email) =>
        SendAsync("forgot-password", new { email });


    public Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword) =>
        SendAsync("reset-password", new { email, code, newPassword });

    
    public Task<AuthResult> GoogleLoginAsync(string idToken) =>
        SendAsync("google-login", new { idToken });

    
    
    public Task<AuthResult> VerifyEmailAsync(string email, string code) =>
        SendAsync("verify-email", new { email, code });


    public Task<AuthResult> ResendVerificationAsync(string email) =>
        SendAsync("resend-verification", new { email });

    // The admin actions send the JWT from login inside the message, so the server knows who is asking
    public async Task<List<UserSummaryDto>?> GetUsersAsync(string token)
    {
        try
        {
            string replyJson = await SendRawAsync("get-users", new { }, token);
            var result = JsonSerializer.Deserialize<UsersResult>(replyJson, JsonOptions);

            // null tells the screen "could not load" - a refusal by the server or a broken answer
            return result is { Success: true } ? result.Users : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> DeleteUserAsync(int id, string token)
    {
        AuthResult result = await SendAsync("delete-user", new { id }, token);
        return result.Success;
    }



        // Our own socket connection to the server (see SocketClient.cs)
    private readonly SocketClient _socket = new();

    // Same JSON options as the server's MessageRouter: camelCase names, reading ignores letter case
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Builds one request message, sends it over the socket and returns the server's answer as JSON text.
    // type = which action ("login"), data = the fields of that action, token = the JWT (empty before login)
    private async Task<string> SendRawAsync(string type, object data, string token = "")
    {
        // Object -> JSON text:  { "type": "login", "token": "", "data": { "email": "...", "password": "..." } }
        string requestJson = JsonSerializer.Serialize(new { type, token, data }, JsonOptions);

        return await _socket.SendTextAsync(requestJson);
    }

    // For the actions whose answer has the usual shape (success, message, role, token...)
    private async Task<AuthResult> SendAsync(string type, object data, string token = "")
    {
        try
        {
            string replyJson = await SendRawAsync(type, data, token);


            // JSON text -> AuthResult object
            var result = JsonSerializer.Deserialize<AuthResult>(replyJson, JsonOptions);
            return result ?? new AuthResult { Success = false, Message = "No response from server." };
        }
        catch (Exception)
        {
            return new AuthResult { Success = false, Message = "Could not reach the server. Is it running?" };
        }
    }



   
}

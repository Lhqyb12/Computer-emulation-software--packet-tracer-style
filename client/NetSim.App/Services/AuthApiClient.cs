using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;


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

// מדבר עם השרת NetSim.Server דרך HTTP
public class AuthApiClient
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://localhost:7089/")

    };

    public Task<AuthResult> RegisterAsync(string username, string email, string password) =>
        PostAsync("api/auth/register", new { username, email, password });


    public Task<AuthResult> LoginAsync(string email, string password) =>
        PostAsync("api/auth/login", new { email, password });

    public Task<AuthResult> ForgotPasswordAsync(string email) =>
        PostAsync("api/auth/forgot-password", new { email });

    public Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword) =>
        PostAsync("api/auth/reset-password", new { email, code, newPassword });

    public Task<AuthResult> GoogleLoginAsync(string idToken) =>
        PostAsync("api/auth/google", new { idToken });


    public Task<AuthResult> VerifyEmailAsync(string email, string code) =>
        PostAsync("api/auth/verify-email", new { email, code });

    public Task<AuthResult> ResendVerificationAsync(string email) =>
        PostAsync("api/auth/resend-verification", new { email });




    // callerEmail הוא האימייל של מי שמחובר כרגע - השרת קורא אותו מתוך ה-Header בשם X-User-Email
    // כדי להחליט אם מותר לגשת לנקודת הקצה הזו (ראי UsersController.GetCallerAsync)
    public async Task<List<UserSummaryDto>?> GetUsersAsync( string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/users");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);


            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> DeleteUserAsync(int id, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"api/users/{id}");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);


            var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<AuthResult> PostAsync(string path, object body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(path, body);//converts the emailand pass to json file, adds the path to the base adress
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "No response from server." };//??=is null
        }
        catch (Exception)
        {
            return new AuthResult { Success = false, Message = "Could not reach the server. Is it running?" };
        }
    }
}

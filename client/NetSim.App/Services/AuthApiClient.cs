using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace NetSim.App.Services;

// הצורה של ה-JSON שהשרת מחזיר
public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

// מדבר עם השרת NetSim.Server דרך HTTP
public class AuthApiClient
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:5184/")
    };

    public Task<AuthResult> RegisterAsync(string username, string email, string password) =>
        PostAsync("api/auth/register", new { username, email, password });

    public Task<AuthResult> LoginAsync(string email, string password) =>
        PostAsync("api/auth/login", new { email, password });

    private async Task<AuthResult> PostAsync(string path, object body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(path, body);
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "No response from server." };
        }
        catch (Exception)
        {
            return new AuthResult { Success = false, Message = "Could not reach the server. Is it running?" };
        }
    }
}

using System;                      // Exception, DateTime
using System.Collections.Generic;  // List<T>
using System.Threading.Tasks;      // Task, async/await
using System.Text.Json;            // JsonSerializer - JSON text <-> C# objects



namespace NetSim.App.Services;

// The shape of the JSON the server sends back (matches AuthResponse on the server)
public class AuthResult
{
    // true = the action succeeded, false = it failed
    public bool Success { get; set; }

    // The text to show the user ("Wrong email or password", "Account created"...)
    public string Message { get; set; } = string.Empty;

    // "User" or "Admin" - filled after a successful login
    public string Role { get; set; } = string.Empty;

    // The JWT - filled after a successful login; sent back with the admin requests
    public string Token { get; set; } = string.Empty;

    // The user's email - filled after a Google sign-in, where the user did not type it
    public string Email { get; set; } = string.Empty;

    // True when the server refused a login only because the email was never verified
    public bool NeedsVerification { get; set; }



}

// One row in the user management table - matches UserSummaryDto on the server side
public class UserSummaryDto
{
    public int Id { get; set; }                           // the user's id in the database
    public string Username { get; set; } = string.Empty;  // the display name
    public string Email { get; set; } = string.Empty;     // the email address
    public DateTime CreatedAt { get; set; }               // when the account was created (UTC)
    public DateTime? LastLoginAt { get; set; }            // the last sign-in (UTC), or null if never
    public string Role { get; set; } = string.Empty;      // "User" or "Admin"
}

// The answer to "get-users" - matches UsersResponse on the server
public class UsersResult
{
    public bool Success { get; set; }                        // true = the list was read
    public string Message { get; set; } = string.Empty;      // the reason, when it failed
    public List<UserSummaryDto> Users { get; set; } = new(); // the users; an empty list by default, never null
}



// Talks to NetSim.Server over our own encrypted TCP socket. Every public method here is one action
// the client can ask the server for; the view models call these and never touch the socket themselves
public class AuthApiClient
{


    // Each of the next seven methods builds one message and sends it:
    // the first argument is the message type, the second is its data.
    // "new { username, email, password }" = a small nameless object with these three fields, which becomes
    // the JSON { "username": "...", "email": "...", "password": "..." }.
    // "=>" = a one-line method that returns the Task that SendAsync returns

    // Purpose: Asks the server to create a new account.
    // Input:   username, email, password - what the user typed on the register screen.
    // Output:  an AuthResult - Success and the Message to show (never throws).
    // Creates a new account
    public Task<AuthResult> RegisterAsync(string username, string email, string password) =>
        SendAsync("register", new { username, email, password });



    // Purpose: Asks the server to sign the user in with an email and a password.
    // Input:   email, password - what the user typed on the login screen.
    // Output:  an AuthResult - on success it holds the Role and the JWT Token; NeedsVerification if the email is not verified.
    // Signs in with an email and a password
    public Task<AuthResult> LoginAsync(string email, string password) =>
        SendAsync("login", new { email, password });


    // Purpose: Asks the server to email a password reset code.
    // Input:   email - the account's email address.
    // Output:  an AuthResult with the Message to show.
    // Asks the server to email a password reset code
    public Task<AuthResult> ForgotPasswordAsync(string email) =>
        SendAsync("forgot-password", new { email });


    // Purpose: Sends the reset code and the new password so the server can change the password.
    // Input:   email - the account; code - the code from the email; newPassword - the new password.
    // Output:  an AuthResult - Success and the Message to show.
    // Sends the reset code together with the new password
    public Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword) =>
        SendAsync("reset-password", new { email, code, newPassword });


    // Purpose: Asks the server to sign the user in with a Google ID token.
    // Input:   idToken - the token GoogleSignInService got from Google.
    // Output:  an AuthResult - on success it holds the Role, the Token and the user's Email.
    // Signs in with the ID token that Google gave us (see GoogleSignInService)
    public Task<AuthResult> GoogleLoginAsync(string idToken) =>
        SendAsync("google-login", new { idToken });



    // Purpose: Sends the email verification code to the server to confirm a new account.
    // Input:   email - the new account's email; code - the code the user typed.
    // Output:  an AuthResult - Success and the Message to show.
    // Sends the email verification code a new user typed
    public Task<AuthResult> VerifyEmailAsync(string email, string code) =>
        SendAsync("verify-email", new { email, code });


    // Purpose: Asks the server to send a new email verification code.
    // Input:   email - the account's email address.
    // Output:  an AuthResult with the Message to show.
    // Asks the server to email a new verification code
    public Task<AuthResult> ResendVerificationAsync(string email) =>
        SendAsync("resend-verification", new { email });

    // Purpose: Asks the server for the list of all users (admin only).
    // Input:   token - the admin's JWT from login.
    // Output:  the list of users, or null if the server refused or could not be reached.
    // The admin actions send the JWT from login inside the message, so the server knows who is asking.
    // Returns the list of all users, or null if it could not be loaded
    public async Task<List<UserSummaryDto>?> GetUsersAsync(string token)
    {
        // Sending or reading the answer can fail, so it runs inside "try"
        try
        {
            // "new { }" = an empty data object: this action needs no details
            string replyJson = await SendRawAsync("get-users", new { }, token);

            // JSON text -> UsersResult object
            var result = JsonSerializer.Deserialize<UsersResult>(replyJson, JsonOptions);

            // null tells the screen "could not load" - a refusal by the server or a broken answer.
            // "result is { Success: true }" = result is not null and its Success field is true
            return result is { Success: true } ? result.Users : null;
        }
        // The server could not be reached, or its answer was not valid JSON
        catch (Exception)
        {
            return null;
        }
    }

    // Purpose: Asks the server to delete one user (admin only).
    // Input:   id - the id of the user to delete; token - the admin's JWT.
    // Output:  true if the user was deleted, otherwise false.
    // Asks the server to delete one user. Returns true if the user was deleted
    public async Task<bool> DeleteUserAsync(int id, string token)
    {
        // Send the id of the user to delete, together with the admin's token
        AuthResult result = await SendAsync("delete-user", new { id }, token);
        return result.Success;
    }



    // Our own socket connection to the server (see SocketClient.cs)
    private readonly SocketClient _socket = new();

    // Same JSON options as the server's MessageRouter: camelCase names, reading ignores letter case
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Purpose: Wraps an action into one JSON request, sends it and returns the raw answer.
    // Input:   type - the action name; data - the action's fields; token - the JWT (optional, empty before login).
    // Output:  the server's answer as JSON text. Throws if the server cannot be reached.
    // Builds one request message, sends it over the socket and returns the server's answer as JSON text.
    // type = which action ("login"), data = the fields of that action, token = the JWT (empty before login)
    private async Task<string> SendRawAsync(string type, object data, string token = "")
    {
        // Object -> JSON text:  { "type": "login", "token": "", "data": { "email": "...", "password": "..." } }
        string requestJson = JsonSerializer.Serialize(new { type, token, data }, JsonOptions);

        // Connect, encrypt, send, and wait for the answer
        return await _socket.SendTextAsync(requestJson);
    }

    // Purpose: Sends one action and turns the answer into an AuthResult.
    // Input:   type - the action name; data - the action's fields; token - the JWT (optional).
    // Output:  an AuthResult; any error becomes a failed result with a message (never throws).
    // For the actions whose answer has the usual shape (success, message, role, token...)
    private async Task<AuthResult> SendAsync(string type, object data, string token = "")
    {
        // The server may be down, so this runs inside "try"
        try
        {
            // Send the message and get the answer as JSON text
            string replyJson = await SendRawAsync(type, data, token);


            // JSON text -> AuthResult object
            var result = JsonSerializer.Deserialize<AuthResult>(replyJson, JsonOptions);

            // "??" = if the answer was the JSON word "null", return a failed result instead of null
            return result ?? new AuthResult { Success = false, Message = "No response from server." };
        }
        // Any error on the way (no connection, a broken answer...) becomes a failed result with a
        // message the screen can show, so the view models never have to catch exceptions themselves
        catch (Exception)
        {
            return new AuthResult { Success = false, Message = "Could not reach the server. Is it running?" };
        }
    }




}

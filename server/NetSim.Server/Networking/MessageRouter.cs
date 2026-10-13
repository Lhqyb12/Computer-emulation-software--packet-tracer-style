using System.Diagnostics;      // Stopwatch - measures how long a request took
using System.Security.Claims;  // ClaimsIdentity and ClaimTypes - the details stored inside a token
using System.Text.Json;        // JsonSerializer - JSON text <-> C# objects
using NetSim.Server.Dtos;      // LoginRequest, AuthResponse and the other message classes
using NetSim.Server.Services;  // AuthService, UserService, TokenService, SecurityLog, ClientInfo

namespace NetSim.Server.Networking;

// Takes one request message, decides which action it asks for, runs it, and returns the answer.
// This is the job the controllers and ASP.NET's routing did for HTTP.
// It also does what RequestLoggingMiddleware did: one log line per request, and catching crashes
public class MessageRouter
{
    // Web defaults = camelCase names ("email", not "Email") and reading that ignores letter case.
    // The client uses the same options, so both sides write and read the same JSON
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // The longest message type written to the log. The type comes from the client, so without a limit
    // anyone could push huge lines into our log
    private const int MaxLoggedTypeLength = 40;

    // Creates a "scope": a fresh set of services (and a fresh database connection) for one request
    private readonly IServiceScopeFactory _scopeFactory;

    // Writes the one-line summary of every request
    private readonly ILogger _requestLogger;

    // Writes exceptions that nobody caught
    private readonly ILogger _crashLogger;

    // The constructor. Dependency injection passes in the two factories
    public MessageRouter(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        // Keep the scope factory for HandleAsync
        _scopeFactory = scopeFactory;

        // Two loggers with fixed category names, so the dashboard can tell their lines apart
        _requestLogger = loggerFactory.CreateLogger(LogCategories.Requests);
        _crashLogger = loggerFactory.CreateLogger(LogCategories.Crashes);
    }

    // Handles one request from start to end and returns the answer as JSON text.
    // requestJson = the decrypted text the client sent,
    // clientIp = the address the connection came from, taken from the socket by SocketServer
    public async Task<string> HandleAsync(string requestJson, string? clientIp)
    {
        // Start measuring how long this request takes
        var stopwatch = Stopwatch.StartNew();

        // The message type for the log. It keeps this value if the message could not even be read
        string type = "(unreadable)";

        // The answer object. Every path below must put a value in it
        object response;

        // Becomes true if an unexpected exception was thrown
        bool crashed = false;

        // Everything risky runs inside "try", so a bad message cannot take the server down
        try
        {
            // JSON text -> RequestMessage object
            RequestMessage? request = JsonSerializer.Deserialize<RequestMessage>(requestJson, JsonOptions);

            // The text was "null", or its "data" part is not a JSON object ({ ... })
            if (request is null || request.Data.ValueKind != JsonValueKind.Object)
            {
                response = Fail("The message must have a type and a data object.");
            }
            // The message has the right shape
            else
            {
                // Keep the type for the log, cut to 40 characters if it is longer. "[..40]" = the first 40 characters
                type = request.Type.Length > MaxLoggedTypeLength ? request.Type[..MaxLoggedTypeLength] : request.Type;

                // The services are Scoped: they need a fresh instance (and a fresh database connection)
                // for every request. ASP.NET opened that scope for each HTTP request; here we open it ourselves.
                // "using var" = the scope and everything in it is cleaned up when this block ends
                using var scope = _scopeFactory.CreateScope();

                // Tell this request's services where the request came from (SecurityLog reads it)
                scope.ServiceProvider.GetRequiredService<ClientInfo>().IpAddress = clientIp;

                // Run the action the message asks for and keep its answer
                response = await RouteAsync(request, scope.ServiceProvider);
            }
        }
        // JsonException = the text is not valid JSON, or a field has the wrong kind of value
        catch (JsonException)
        {
            // The client sent something that is not the JSON we expect
            response = Fail("The message is not valid.");
        }
        // Any other exception
        catch (Exception ex)
        {
            // Something threw and nobody caught it - a bug, or something the code did not expect
            // (database down, mail server refusing...). The full exception goes to the log and to the
            // dashboard's Errors tab. The client gets a short, generic answer: the exception text reveals
            // file names, table names and how the code works - a gift to an attacker
            _crashLogger.LogError(ex, "{Type}", type);
            response = Fail("Internal server error.");
            crashed = true;
        }

        // Stop measuring
        stopwatch.Stop();

        // One line per request. Only the type is logged, never the data - the data can hold a password.
        // The result decides the severity: a crash = Error, a refusal = Warning, anything else = Information.
        // "response is X { Success: true }" = the answer is of class X and its Success field is true
        bool success = response is AuthResponse { Success: true } or UsersResponse { Success: true };

        // Two "? :" in a row: crashed -> Error, otherwise success -> Information, otherwise Warning
        LogLevel level = crashed ? LogLevel.Error : success ? LogLevel.Information : LogLevel.Warning;

        // The same decision, as a word for the log line
        string result = crashed ? "crashed" : success ? "ok" : "refused";

        // For example: "login -> ok (42 ms) from 127.0.0.1". "??" = use "-" if the address is null
        _requestLogger.Log(level, "{Type} -> {Result} ({Elapsed} ms) from {Ip}",
            type, result, stopwatch.ElapsedMilliseconds, clientIp ?? "-");

        // Object -> JSON text
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    // The routing itself: which action does this message ask for?
    // services = the fresh set of services that HandleAsync opened for this request
    private static async Task<object> RouteAsync(RequestMessage request, IServiceProvider services)
    {
        // Ask the scope for an AuthService; most actions below need it
        var auth = services.GetRequiredService<AuthService>();

        // Choose the block that matches the message type
        switch (request.Type)
        {
            // Every case does the same two steps: turn the raw "data" JSON into the right request class
            // ("!" = we promise the result is not null), and pass it to the matching method
            case "login":
                var login = request.Data.Deserialize<LoginRequest>(JsonOptions)!;
                return await auth.LoginAsync(login);

            case "register":
                var register = request.Data.Deserialize<RegisterRequest>(JsonOptions)!;
                return await auth.RegisterAsync(register);

            case "verify-email":
                var verify = request.Data.Deserialize<VerifyEmailRequest>(JsonOptions)!;
                return await auth.VerifyEmailAsync(verify);

            case "resend-verification":
                var resend = request.Data.Deserialize<ResendVerificationRequest>(JsonOptions)!;
                return await auth.ResendVerificationAsync(resend);

            case "forgot-password":
                var forgot = request.Data.Deserialize<ForgotPasswordRequest>(JsonOptions)!;
                return await auth.ForgotPasswordAsync(forgot);

            case "reset-password":
                var reset = request.Data.Deserialize<ResetPasswordRequest>(JsonOptions)!;
                return await auth.ResetPasswordAsync(reset);

            case "google-login":
                var google = request.Data.Deserialize<GoogleLoginRequest>(JsonOptions)!;
                return await auth.GoogleLoginAsync(google);

            // The list of all users - for admins only
            case "get-users":
            {
                // Check the token. null = not an admin
                ClaimsIdentity? admin = await RequireAdminAsync(services, request);
                if (admin is null)
                {
                    return Fail("Not authorized.");
                }

                // Read all the users from the database and return them
                var users = services.GetRequiredService<UserService>();
                return new UsersResponse { Success = true, Users = await users.GetAllAsync() };
            }

            // Deleting one user - for admins only
            case "delete-user":
            {
                // Check the token. null = not an admin
                ClaimsIdentity? admin = await RequireAdminAsync(services, request);
                if (admin is null)
                {
                    return Fail("Not authorized.");
                }

                // The data holds the id of the user to delete
                var delete = request.Data.Deserialize<DeleteUserRequest>(JsonOptions)!;
                var users = services.GetRequiredService<UserService>();

                // Who is asking is read from the verified token, not from the message's data.
                // "?.Value" = the value of the claim, or null if the token has no such claim
                string? callerId = admin.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string? adminEmail = admin.FindFirst(ClaimTypes.Email)?.Value;

                // Delete the user (UserService refuses if an admin tries to delete themselves)
                return await users.DeleteAsync(delete.Id, callerId, adminEmail);
            }

            // A type we do not know
            default:
                return Fail("Unknown message type.");
        }
    }

    // The gate in front of every admin action. Returns the admin's identity, or null if the request
    // must be refused. This replaces [Authorize(Roles = "Admin")] and the JWT middleware of ASP.NET
    private static async Task<ClaimsIdentity?> RequireAdminAsync(IServiceProvider services, RequestMessage request)
    {
        // The two services this check needs
        var tokens = services.GetRequiredService<TokenService>();
        var security = services.GetRequiredService<SecurityLog>();

        // Step 1 - authentication: is the token real? (signed by us, not edited, not expired)
        ClaimsIdentity? identity = await tokens.ValidateTokenAsync(request.Token);

        // null = the token failed the check
        if (identity is null)
        {
            // Choose the text for the security log: no token at all, or a bad one
            string reason = string.IsNullOrWhiteSpace(request.Token)
                ? "No token was sent"
                : "The token is not valid or has expired";

            // Write it to the security log. null = we do not know who this was
            await security.RecordAsync("Unauthorized request", null, $"{request.Type} - {reason}");
            return null;
        }

        // Step 2 - authorization: the token is real, so we know who this is. Are they an admin?
        string? role = identity.FindFirst(ClaimTypes.Role)?.Value;

        // A real user, but not an admin
        if (role != "Admin")
        {
            // Write who tried, and what they tried, to the security log
            string? email = identity.FindFirst(ClaimTypes.Email)?.Value;
            await security.RecordAsync("Access denied", email, $"Role '{role}' tried {request.Type}");
            return null;
        }

        // A verified admin
        return identity;
    }

    // A short way to build a "failed" answer with a message. "=>" = a one-line method that returns this value
    private static AuthResponse Fail(string message) => new AuthResponse { Success = false, Message = message };
}

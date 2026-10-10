using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using NetSim.Server.Dtos;
using NetSim.Server.Services;

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

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _requestLogger;
    private readonly ILogger _crashLogger;

    public MessageRouter(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _requestLogger = loggerFactory.CreateLogger(LogCategories.Requests);
        _crashLogger = loggerFactory.CreateLogger(LogCategories.Crashes);
    }

    // clientIp = the address the connection came from, taken from the socket by SocketServer
    public async Task<string> HandleAsync(string requestJson, string? clientIp)
    {
        var stopwatch = Stopwatch.StartNew();
        string type = "(unreadable)";
        object response;
        bool crashed = false;

        try
        {
            // JSON text -> RequestMessage object
            RequestMessage? request = JsonSerializer.Deserialize<RequestMessage>(requestJson, JsonOptions);
            if (request is null || request.Data.ValueKind != JsonValueKind.Object)
            {
                response = Fail("The message must have a type and a data object.");
            }
            else
            {
                type = request.Type.Length > MaxLoggedTypeLength ? request.Type[..MaxLoggedTypeLength] : request.Type;

                // The services are Scoped: they need a fresh instance (and a fresh database connection)
                // for every request. ASP.NET opened that scope for each HTTP request; here we open it ourselves
                using var scope = _scopeFactory.CreateScope();

                // Tell this request's services where the request came from (SecurityLog reads it)
                scope.ServiceProvider.GetRequiredService<ClientInfo>().IpAddress = clientIp;

                response = await RouteAsync(request, scope.ServiceProvider);
            }
        }
        catch (JsonException)
        {
            // The client sent something that is not the JSON we expect
            response = Fail("The message is not valid.");
        }
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

        stopwatch.Stop();

        // One line per request. Only the type is logged, never the data - the data can hold a password.
        // The result decides the severity: a crash = Error, a refusal = Warning, anything else = Information
        bool success = response is AuthResponse { Success: true } or UsersResponse { Success: true };
        LogLevel level = crashed ? LogLevel.Error : success ? LogLevel.Information : LogLevel.Warning;
        string result = crashed ? "crashed" : success ? "ok" : "refused";

        _requestLogger.Log(level, "{Type} -> {Result} ({Elapsed} ms) from {Ip}",
            type, result, stopwatch.ElapsedMilliseconds, clientIp ?? "-");

        // Object -> JSON text
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    // The routing itself: which action does this message ask for?
    private static async Task<object> RouteAsync(RequestMessage request, IServiceProvider services)
    {
        var auth = services.GetRequiredService<AuthService>();

        switch (request.Type)
        {
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

            case "get-users":
            {
                ClaimsIdentity? admin = await RequireAdminAsync(services, request);
                if (admin is null)
                {
                    return Fail("Not authorized.");
                }

                var users = services.GetRequiredService<UserService>();
                return new UsersResponse { Success = true, Users = await users.GetAllAsync() };
            }

            case "delete-user":
            {
                ClaimsIdentity? admin = await RequireAdminAsync(services, request);
                if (admin is null)
                {
                    return Fail("Not authorized.");
                }

                var delete = request.Data.Deserialize<DeleteUserRequest>(JsonOptions)!;
                var users = services.GetRequiredService<UserService>();

                // Who is asking is read from the verified token, not from the message's data
                string? callerId = admin.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string? adminEmail = admin.FindFirst(ClaimTypes.Email)?.Value;
                return await users.DeleteAsync(delete.Id, callerId, adminEmail);
            }

            default:
                return Fail("Unknown message type.");
        }
    }

    // The gate in front of every admin action. Returns the admin's identity, or null if the request
    // must be refused. This replaces [Authorize(Roles = "Admin")] and the JWT middleware of ASP.NET
    private static async Task<ClaimsIdentity?> RequireAdminAsync(IServiceProvider services, RequestMessage request)
    {
        var tokens = services.GetRequiredService<TokenService>();
        var security = services.GetRequiredService<SecurityLog>();

        // Step 1 - authentication: is the token real? (signed by us, not edited, not expired)
        ClaimsIdentity? identity = await tokens.ValidateTokenAsync(request.Token);
        if (identity is null)
        {
            string reason = string.IsNullOrWhiteSpace(request.Token)
                ? "No token was sent"
                : "The token is not valid or has expired";
            await security.RecordAsync("Unauthorized request", null, $"{request.Type} - {reason}");
            return null;
        }

        // Step 2 - authorization: the token is real, so we know who this is. Are they an admin?
        string? role = identity.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin")
        {
            string? email = identity.FindFirst(ClaimTypes.Email)?.Value;
            await security.RecordAsync("Access denied", email, $"Role '{role}' tried {request.Type}");
            return null;
        }

        return identity;
    }

    private static AuthResponse Fail(string message) => new AuthResponse { Success = false, Message = message };
}

using Microsoft.EntityFrameworkCore;  // UseNpgsql / AddDbContext - connects EF Core to the PostgreSQL database
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Services;         // AuthService
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Avalonia;
using NetSim.Server.Dashboard;
using NetSim.Server.Middleware;






// "Top-level statements" (a C# 9+ feature): there's no explicit class Program with a static void Main here -
// the compiler generates that behind the scenes. This is the standard Minimal Hosting Model style used in new
// ASP.NET Core projects. "args" are command-line arguments, passed automatically into CreateBuilder
var builder = WebApplication.CreateBuilder(args);


// The Super Admin dashboard's log: one store object, created here by hand because two different places
// need the very same instance - the logging system writes into it, and the dashboard window reads from it.
// AddSingleton(logStore) puts it in the DI container so the dashboard can ask for it later.
// AddProvider plugs our provider into ASP.NET Core's logging, next to the built-in Console one
var logStore = new DashboardLogStore();
builder.Services.AddSingleton(logStore);
builder.Logging.AddProvider(new DashboardLoggerProvider(logStore));


// Controllers host the API surface (see Controllers/). OpenAPI/Swagger is dev-only.
// AddControllers registers everything the DI container needs so that [ApiController] and attribute routing work
builder.Services.AddControllers();
// AddOpenApi generates API documentation (OpenAPI/Swagger) automatically from the code - useful for manual
// testing (Swagger UI) during development
builder.Services.AddOpenApi();

// Registers AppDbContext with the DI container as a "service" - every request that asks for AppDbContext
// (e.g. AuthService) gets an instance
// options.UseNpgsql(...) tells EF Core the provider is PostgreSQL (not SQL Server/SQLite etc.)
// GetConnectionString("Default") reads the "ConnectionStrings:Default" key from configuration - loaded from
// appsettings.json + user-secrets (in development) + environment variables, so the connection string
// (with the password!) never lives in the code/git at all
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// AddScoped: a new AuthService instance is created for every HTTP request (not one global Singleton, and not
// a brand-new one on every single injection like Transient). This has to be Scoped (not Singleton) because
// AuthService depends on AppDbContext, which is itself Scoped by default - DbContext is not thread-safe, and
// a single instance of it must never be shared across multiple concurrent requests/threads
builder.Services.AddScoped<AuthService>();
//builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailSender>();

// SecurityLog records security events (see Services/SecurityLog.cs). It needs to know which request is
// being handled right now, to read the caller's IP address - AddHttpContextAccessor makes that available
// to classes that are not controllers
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecurityLog>();

builder.Services.AddScoped<TokenService>();




string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
        };

        // Hooks the JWT middleware calls at specific moments. We use two of them to record, in the
        // security log, every time a protected endpoint ([Authorize]) turned a request away
        options.Events = new JwtBearerEvents
        {
            // "Challenge" = the request did not prove who it is: no token at all, or a token that is
            // expired / forged / damaged. The client gets 401
            OnChallenge = async context =>
            {
                string reason = context.AuthenticateFailure switch
                {
                    null => "No token was sent",
                    SecurityTokenExpiredException => "The token has expired",
                    _ => "The token is not valid",
                };

                // RequestServices = the DI container of this specific request
                var security = context.HttpContext.RequestServices.GetRequiredService<SecurityLog>();
                await security.RecordAsync("Unauthorized request", null,
                    $"{context.Request.Method} {context.Request.Path} - {reason}");
            },

            // "Forbidden" = the token is fine, so we know exactly who this is - but their role is not
            // allowed here (a regular User calling an Admin-only endpoint). The client gets 403
            OnForbidden = async context =>
            {
                string? email = context.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                string? role = context.HttpContext.User.FindFirstValue(ClaimTypes.Role);

                var security = context.HttpContext.RequestServices.GetRequiredService<SecurityLog>();
                await security.RecordAsync("Access denied", email,
                    $"Role '{role}' tried {context.Request.Method} {context.Request.Path}");
            },
        };
    });

builder.Services.AddAuthorization();




// The "Build" step: everything registered above in builder.Services gets "locked in" to a ready-to-run app
var app = builder.Build();

// One-time (per startup) admin bootstrap: if an "AdminSeed:Email" is configured (via dotnet user-secrets,
// never committed to git), and that user exists in the DB and isn't already an Admin, promote them.
// This lets us create the very first Admin without ever touching the database by hand - just configuration.
string? adminSeedEmail = builder.Configuration["AdminSeed:Email"];
if (!string.IsNullOrWhiteSpace(adminSeedEmail))
{
    // CreateScope is needed here because AppDbContext is Scoped (see the registration above) - it can only be
    // resolved from inside a scope, and outside of a request there is no scope automatically available,
    // so we create one manually just for this one-off startup task
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == adminSeedEmail);
    if (adminUser is not null && adminUser.Role != "Admin")
    {
        adminUser.Role = "Admin";
        await db.SaveChangesAsync();
    }
}


// IsDevelopment checks the ASPNETCORE_ENVIRONMENT environment variable. The OpenAPI docs are exposed only in
// development - we don't want a production environment to expose the full endpoint map and internal API shape to anyone
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // A deliberate crash, for testing the dashboard's Errors tab: open https://localhost:7089/debug/crash
    // in a browser. Inside the IsDevelopment block on purpose - it must never exist in production
    app.MapGet("/debug/crash", string () =>
        throw new InvalidOperationException("Test crash: thrown on purpose by /debug/crash."));

}

// The server listens on HTTPS only (see Properties/launchSettings.json), so email+password and the
// JWT never travel as plain text. This redirect is a second layer: if an HTTP address is ever added,
// requests to it are sent to HTTPS. It is not the protection itself - a redirect only answers after
// the plain request has already arrived.

// First in the pipeline on purpose: it wraps everything after it, so it sees every request and measures
// the whole time the server spent on it - including requests that are refused by authentication
app.UseMiddleware<RequestLoggingMiddleware>();


app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Simple liveness probe – lets the client (and us) confirm the server is up.
// A deliberately minimal, unauthenticated endpoint (no [ApiController]/DTOs) - "MapGet" directly on the app,
// meant for monitoring/deployment checks (e.g. a Docker healthcheck) that only need to know "the server is
// alive", without exposing any sensitive information
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "NetSim.Server" }))
   .WithName("Health");

// Enables routing to all the controllers marked with [ApiController]/[Route] (here: AuthController) -
// without this line, /api/auth/register and /api/auth/login wouldn't be reachable at all
app.MapControllers();

// Starts the web server (Kestrel) and starts listening for requests - a blocking call that keeps running
// until the process is stopped

// Start() instead of Run(): Run blocks until the server stops, so nothing after it would ever execute.
// StartAsync starts Kestrel listening in the background and returns immediately, which leaves this
// thread free to open the Super Admin window
await app.StartAsync();


// The Super Admin window runs on its own thread, next to the web server.
// A desktop UI on Windows needs an "STA" thread (the client gets this from [STAThread] on Main);
// here Main is async, so we create a dedicated thread and mark it STA ourselves
var uiThread = new Thread(() =>
{
        AppBuilder.Configure(() => new DashboardApp(app.Services))

        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args);
});
if (OperatingSystem.IsWindows())
{
    uiThread.SetApartmentState(ApartmentState.STA);
}
uiThread.Start();

// Join waits here until the window is closed - closing the window is what shuts the server down
uiThread.Join();

await app.StopAsync();


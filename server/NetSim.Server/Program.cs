using Microsoft.EntityFrameworkCore;  // UseNpgsql / AddDbContext - connects EF Core to the PostgreSQL database
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Services;         // AuthService


// "Top-level statements" (a C# 9+ feature): there's no explicit class Program with a static void Main here -
// the compiler generates that behind the scenes. This is the standard Minimal Hosting Model style used in new
// ASP.NET Core projects. "args" are command-line arguments, passed automatically into CreateBuilder
var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailSender>();



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
}

// Forces every plain HTTP request to be redirected to HTTPS -
// security-critical: without this, email+password could travel over the network as plain text and be
// intercepted (man-in-the-middle)
app.UseHttpsRedirection();

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
app.Run();

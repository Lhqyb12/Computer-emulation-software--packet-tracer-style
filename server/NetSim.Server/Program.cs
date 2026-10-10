using Avalonia;
using Microsoft.EntityFrameworkCore;  // UseNpgsql / AddDbContext - connects EF Core to the PostgreSQL database
using NetSim.Server.Dashboard;
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Networking;       // SocketServer, MessageRouter, ServerKeys
using NetSim.Server.Services;         // AuthService and the other services

// "Top-level statements" (a C# 9+ feature): there's no explicit class Program with a static void Main here -
// the compiler generates that behind the scenes.
//
// Host.CreateApplicationBuilder builds the plain .NET "host": configuration (appsettings.json + user-secrets),
// logging and the DI container. It is what is left of the old WebApplication builder once the web server is
// taken out - no Kestrel, no HTTP, no controllers. All the communication with clients is our own SocketServer
var builder = Host.CreateApplicationBuilder(args);

// The Super Admin dashboard's log: one store object, created here by hand because two different places
// need the very same instance - the logging system writes into it, and the dashboard window reads from it.
// AddSingleton(logStore) puts it in the DI container so the dashboard can ask for it later.
// AddProvider plugs our provider into the logging system, next to the built-in Console one
var logStore = new DashboardLogStore();
builder.Services.AddSingleton(logStore);
builder.Logging.AddProvider(new DashboardLoggerProvider(logStore));

// Registers AppDbContext with the DI container as a "service" - every request that asks for AppDbContext
// (e.g. AuthService) gets an instance
// options.UseNpgsql(...) tells EF Core the provider is PostgreSQL (not SQL Server/SQLite etc.)
// GetConnectionString("Default") reads the "ConnectionStrings:Default" key from configuration - loaded from
// appsettings.json + user-secrets (in development) + environment variables, so the connection string
// (with the password!) never lives in the code/git at all
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// AddScoped: a new instance is created for every request (MessageRouter opens a scope per message).
// These have to be Scoped (not Singleton) because they depend on AppDbContext, which is itself Scoped -
// DbContext is not thread-safe, and a single instance of it must never be shared across concurrent requests
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();

// ClientInfo holds the address of the client behind the current request; SecurityLog reads it
builder.Services.AddScoped<ClientInfo>();
builder.Services.AddScoped<SecurityLog>();

// Our own communication layer: one of each for the whole life of the server
builder.Services.AddSingleton<ServerKeys>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<SocketServer>();

// Fail at startup, with a clear message, rather than on the first login
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
{
    throw new InvalidOperationException("Jwt:Key is not configured.");
}

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

// Starts the host. StartAsync returns immediately, which leaves this thread free to continue
await app.StartAsync();

// Start our socket server. ApplicationStopping is the signal that fires when the server shuts down -
// it is what ends the server's Accept loop
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var socketServer = app.Services.GetRequiredService<SocketServer>();
_ = socketServer.RunAsync(lifetime.ApplicationStopping);

// The Super Admin window runs on its own thread, next to the socket server.
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

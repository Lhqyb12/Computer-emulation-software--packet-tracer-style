using NetSim.Server.Data;    // AppDbContext
using NetSim.Server.Models;  // SecurityEvent

namespace NetSim.Server.Services;

// The one place that records security events. Any part of the server that notices something
// security-related calls RecordAsync, and the event is:
//  1. saved as a row in the SecurityEvents table (permanent history)
//  2. written to the log under the "NetSim.Security" category - so it also appears in the dashboard's
//     Logs tab, and tells the dashboard to refresh its Security tab
public class SecurityLog
{
    // The log category of security events. A constant, so the dashboard recognises it by the same name
    public const string Category = "NetSim.Security";

    // Longest text accepted from the client for one field. The email comes straight from the client's
    // message, so without a limit anyone could fill the table with megabytes of junk in a single request
    private const int MaxFieldLength = 256;

    // Creates a "scope": a fresh set of services with its own database connection
    private readonly IServiceScopeFactory _scopeFactory;

    // What we know about the client behind the current request (its IP address)
    private readonly ClientInfo _client;

    // Writes the event to the log, under the "NetSim.Security" category
    private readonly ILogger _logger;

    // The constructor. Dependency injection passes in the three objects
    public SecurityLog(IServiceScopeFactory scopeFactory, ClientInfo client, ILoggerFactory loggerFactory)

    {
        _scopeFactory = scopeFactory;
        _client = client;

        // Create a logger whose lines carry our fixed category name
        _logger = loggerFactory.CreateLogger(Category);
    }

    // Records one security event.
    // eventType = a short fixed name ("Login failed"), email = who it is about (null if unknown),
    // details = the specific reason, for the owner's eyes only
    public async Task RecordAsync(string eventType, string? email, string details)
    {
        // The address the request came from. The router wrote it into ClientInfo when the message arrived
        string? ip = _client.IpAddress;

        // An email longer than the limit is cut down to the limit
        if (email is not null && email.Length > MaxFieldLength)
        {
            // "[..256]" = the first 256 characters
            email = email[..MaxFieldLength];
        }

        // A separate scope = a separate AppDbContext, not the one the request itself is using.
        // The event is saved on its own, independent of whatever the request is in the middle of:
        // it is recorded even if the request's own database work fails, and saving it can never
        // accidentally save half-finished changes of the request
        using var scope = _scopeFactory.CreateScope();

        // Ask that scope for its own database connection
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Build the new row and mark it to be inserted. Id and OccurredAt are filled in automatically
        db.SecurityEvents.Add(new SecurityEvent
        {
            EventType = eventType,
            Email = email,
            IpAddress = ip,
            Details = details,
        });

        // Run the INSERT statement
        await db.SaveChangesAsync();

        // Warning level: more important than an ordinary request line, and shown in yellow in the Logs tab.
        // "??" = write "-" where the email or the address is unknown
        _logger.LogWarning("{EventType} | {Email} | {Details} | from {Ip}", eventType, email ?? "-", details, ip ?? "-");
    }
}

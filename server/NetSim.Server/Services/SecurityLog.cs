using NetSim.Server.Data;
using NetSim.Server.Models;

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

    // Longest text accepted from the client for one field. The email comes straight from the request
    // body, so without a limit anyone could fill the table with megabytes of junk in a single request
    private const int MaxFieldLength = 256;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClientInfo _client;

    private readonly ILogger _logger;

    public SecurityLog(IServiceScopeFactory scopeFactory, ClientInfo client, ILoggerFactory loggerFactory)

    {
        _scopeFactory = scopeFactory;
        _client = client;
        _logger = loggerFactory.CreateLogger(Category);
    }

    public async Task RecordAsync(string eventType, string? email, string details)
    {
        // The address the request came from. The router wrote it into ClientInfo when the message arrived
        string? ip = _client.IpAddress;

        if (email is not null && email.Length > MaxFieldLength)
        {
            email = email[..MaxFieldLength];
        }

        // A separate scope = a separate AppDbContext, not the one the request itself is using.
        // The event is saved on its own, independent of whatever the request is in the middle of:
        // it is recorded even if the request's own database work fails, and saving it can never
        // accidentally save half-finished changes of the request
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.SecurityEvents.Add(new SecurityEvent
        {
            EventType = eventType,
            Email = email,
            IpAddress = ip,
            Details = details,
        });
        await db.SaveChangesAsync();

        // Warning level: more important than an ordinary request line, and shown in yellow in the Logs tab
        _logger.LogWarning("{EventType} | {Email} | {Details} | from {Ip}", eventType, email ?? "-", details, ip ?? "-");
    }
}

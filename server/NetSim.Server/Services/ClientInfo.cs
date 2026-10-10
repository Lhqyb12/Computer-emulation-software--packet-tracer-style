namespace NetSim.Server.Services;

// What we know about the client behind the request being handled right now.
// Scoped: every request gets its own instance. The router fills it in when a message arrives,
// and SecurityLog reads it. This replaces HttpContext, which ASP.NET kept for every HTTP request
public class ClientInfo
{
    // The address the connection came from, taken from the socket
    public string? IpAddress { get; set; }
}

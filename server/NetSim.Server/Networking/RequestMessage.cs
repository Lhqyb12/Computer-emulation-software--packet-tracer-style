using System.Text.Json;

namespace NetSim.Server.Networking;

// The shape of every message a client sends us, written as JSON inside the frame:
//   { "type": "login", "data": { "email": "...", "password": "..." } }
public class RequestMessage
{
    // Which action the client asks for: "login", "register", ...
    public string Type { get; set; } = string.Empty;

    // The JWT the client received at login. Empty for actions that need no login (like "login" itself).
    // This replaces the HTTP header "Authorization: Bearer ..."
    public string Token { get; set; } = string.Empty;

    // The details of that action. Every action has different fields, so at this point the data
    // stays as raw JSON; the router turns it into the right class once it knows the type
    public JsonElement Data { get; set; }
}

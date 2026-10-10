namespace NetSim.Server.Networking;

// The names two kinds of log messages are written under. Constants, so the router that writes them
// and the dashboard that looks for them use the same name without the text being typed twice
// (a typo in one place would silently break the dashboard)
public static class LogCategories
{
    // One line per handled request - the dashboard counts these for "Requests (last hour)"
    public const string Requests = "NetSim.Requests";

    // An exception nobody handled - the dashboard shows these in its Errors tab
    public const string Crashes = "NetSim.Crashes";
}

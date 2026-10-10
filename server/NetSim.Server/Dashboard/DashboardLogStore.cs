namespace NetSim.Server.Dashboard;

// One log message exactly as the server produced it (before it is turned into text for the table)
public record LogEntry(DateTime Time, LogLevel Level, string Category, string Message, Exception? Exception);

// The server's "memory" of its latest log messages - the meeting point between the two halves of the
// process: the web server writes into it (from many threads at once), the dashboard window reads from it.
// Registered as a Singleton: one single instance for the whole lifetime of the server
public class DashboardLogStore
{
    // Only the newest messages are kept, so a server that runs for days doesn't fill up the memory
    private const int MaxEntries = 500;

    private readonly List<LogEntry> _entries = new();

    // Several requests can log at the same moment, each on its own thread. "lock (_gate)" lets only
    // one thread at a time touch the list - without it two threads adding together could corrupt it
    private readonly object _gate = new();

    // Whoever wants to hear about every new message (the dashboard). null = nobody is listening yet
    private Action<LogEntry>? _listener;

    // Called by the logger for every message
    public void Add(LogEntry entry)
    {
        Action<LogEntry>? listener;
        lock (_gate)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);   // drop the oldest
            }
            listener = _listener;
        }

        // Outside the lock on purpose: the listener's code must not be able to hold up other threads
        listener?.Invoke(entry);
    }

    // Called once by the dashboard when its window opens. Returns everything logged so far (the server
    // starts before the window, so some messages already exist) and from now on calls "listener" for
    // each new message. Both happen under the same lock, so no message is missed or received twice
    public LogEntry[] Listen(Action<LogEntry> listener)
    {
        lock (_gate)
        {
            _listener = listener;
            return _entries.ToArray();
        }
    }
}

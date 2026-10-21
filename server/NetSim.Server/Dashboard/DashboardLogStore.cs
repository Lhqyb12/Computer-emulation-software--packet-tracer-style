namespace NetSim.Server.Dashboard;

// One log message exactly as the server produced it (before it is turned into text for the table).
// "record" = a short way to declare a class that only carries data: the compiler creates the
// constructor and the read-only properties from the list in the parentheses
public record LogEntry(DateTime Time, LogLevel Level, string Category, string Message, Exception? Exception);

// The server's "memory" of its latest log messages - the meeting point between the two halves of the
// process: the socket server writes into it (from many threads at once), the dashboard window reads from it.
// Registered as a Singleton: one single instance for the whole lifetime of the server
public class DashboardLogStore
{
    // Only the newest messages are kept, so a server that runs for days doesn't fill up the memory
    private const int MaxEntries = 500;

    // The messages themselves, oldest first
    private readonly List<LogEntry> _entries = new();

    // Several requests can log at the same moment, each on its own thread. "lock (_gate)" lets only
    // one thread at a time touch the list - without it two threads adding together could corrupt it
    private readonly object _gate = new();

    // Whoever wants to hear about every new message (the dashboard). null = nobody is listening yet.
    // "Action<LogEntry>" = a method that receives a LogEntry and returns nothing
    private Action<LogEntry>? _listener;

    // Purpose: Stores one log message (keeping only the newest 500) and passes it to the listener.
    // Input:   entry - the log message.
    // Output:  nothing.
    // Called by the logger for every message
    public void Add(LogEntry entry)
    {
        // A local copy of the listener, so it can be called after the lock is released
        Action<LogEntry>? listener;

        // Only one thread at a time may run this block
        lock (_gate)
        {
            // Add the new message at the end
            _entries.Add(entry);

            // Too many messages?
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);   // drop the oldest
            }

            // Copy the listener while we still hold the lock
            listener = _listener;
        }

        // Outside the lock on purpose: the listener's code must not be able to hold up other threads.
        // "?.Invoke" = call it only if somebody is listening
        listener?.Invoke(entry);
    }

    // Purpose: Registers the dashboard as the listener for new messages and returns the old ones.
    // Input:   listener - the method to call for every new message.
    // Output:  a copy of every message stored so far, oldest first.
    // Called once by the dashboard when its window opens. Returns everything logged so far (the server
    // starts before the window, so some messages already exist) and from now on calls "listener" for
    // each new message. Both happen under the same lock, so no message is missed or received twice
    public LogEntry[] Listen(Action<LogEntry> listener)
    {
        lock (_gate)
        {
            // Remember who to call from now on
            _listener = listener;

            // ToArray = a copy of the list as it is right now, so later changes do not affect the caller
            return _entries.ToArray();
        }
    }
}

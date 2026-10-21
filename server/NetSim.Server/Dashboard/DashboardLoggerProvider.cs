namespace NetSim.Server.Dashboard;

// .NET sends every log message to all the registered "logger providers". The built-in Console
// provider prints them in the black terminal window; this provider is ours, and it puts them in the
// DashboardLogStore so the Super Admin window can show them. Nothing in the rest of the server has to
// change - any code that already logs with ILogger reaches the dashboard automatically.
// ": ILoggerProvider" = this class promises to have the methods the logging system expects from a provider
public class DashboardLoggerProvider : ILoggerProvider
{
    // Where the messages are kept
    private readonly DashboardLogStore _store;

    // Purpose: Builds the logger provider that writes into the dashboard's store.
    // Input:   store - the one shared DashboardLogStore.
    // Output:  none (a constructor) - a new DashboardLoggerProvider.
    // The constructor. Program.cs passes in the one shared store
    public DashboardLoggerProvider(DashboardLogStore store)
    {
        _store = store;
    }

    // Purpose: Creates a logger for one category that writes into the store.
    // Input:   categoryName - the name of the part of the system that logs.
    // Output:  a new DashboardLogger for that category.
    // Called once per "category" - the name of the part of the system that writes the message
    // (e.g. "NetSim.Requests", "Microsoft.EntityFrameworkCore.Database.Command").
    // Returns a logger that remembers its category and writes into our store
    public ILogger CreateLogger(string categoryName) => new DashboardLogger(categoryName, _store);

    // Purpose: Required by ILoggerProvider; there is nothing to clean up.
    // Input:   nothing.
    // Output:  nothing.
    // ILoggerProvider requires a Dispose method; we have nothing to clean up, so it is empty
    public void Dispose()
    {
    }

    // The logger itself. "private class" inside another class = only DashboardLoggerProvider can use it
    private class DashboardLogger : ILogger
    {
        // The category this logger writes under
        private readonly string _category;

        // Where the messages are kept
        private readonly DashboardLogStore _store;

        // Purpose: Builds a logger for one category.
        // Input:   category - the category name; store - where the messages are kept.
        // Output:  none (a constructor) - a new DashboardLogger.
        // The constructor: keep both values
        public DashboardLogger(string category, DashboardLogStore store)
        {
            _category = category;
            _store = store;
        }

        // Purpose: Required by ILogger; scopes are not used, so it does nothing.
        // Input:   state - the scope's data (ignored).
        // Output:  null.
        // Scopes (extra context attached to a group of messages) are not used by the dashboard,
        // so this required method simply returns null
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Purpose: Decides whether a message of this level and category reaches the dashboard.
        // Input:   logLevel - how serious the message is.
        // Output:  true if the message should be stored, otherwise false.
        // Which messages reach the dashboard:
        //  - warnings and errors from anywhere
        //  - ordinary "Information" messages only from our own code (categories starting with "NetSim")
        //    and from the server's start/stop messages (Microsoft.Hosting).
        // Without this filter the table would drown in technical noise - EF Core alone writes the
        // full SQL text of every single database query at Information level
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Warning
            || _category.StartsWith("NetSim")
            || _category.StartsWith("Microsoft.Hosting");

        // Purpose: Builds the final text of one message and stores it, if the filter lets it through.
        // Input:   logLevel, eventId, state, exception, formatter - the message's parts from the logging system.
        // Output:  nothing.
        // Called by the logging system for every message written under this category.
        // logLevel = how serious it is, state = the message template with its values,
        // exception = the exception that came with it (or null), formatter = builds the final text.
        // eventId is part of the required signature; we do not use it
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Skip the messages the filter above does not let through
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // formatter builds the final text out of the message template and its values.
            // The message is stored together with the current time, its level and its category
            _store.Add(new LogEntry(DateTime.Now, logLevel, _category, formatter(state, exception), exception));
        }
    }
}

namespace NetSim.Server.Dashboard;

// ASP.NET Core sends every log message to all the registered "logger providers". The built-in Console
// provider prints them in the black terminal window; this provider is ours, and it puts them in the
// DashboardLogStore so the Super Admin window can show them. Nothing in the rest of the server has to
// change - any code that already logs with ILogger reaches the dashboard automatically
public class DashboardLoggerProvider : ILoggerProvider
{
    private readonly DashboardLogStore _store;

    public DashboardLoggerProvider(DashboardLogStore store)
    {
        _store = store;
    }

    // Called once per "category" - the name of the part of the system that writes the message
    // (e.g. "NetSim.Requests", "Microsoft.EntityFrameworkCore.Database.Command")
    public ILogger CreateLogger(string categoryName) => new DashboardLogger(categoryName, _store);

    public void Dispose()
    {
    }

    private class DashboardLogger : ILogger
    {
        private readonly string _category;
        private readonly DashboardLogStore _store;

        public DashboardLogger(string category, DashboardLogStore store)
        {
            _category = category;
            _store = store;
        }

        // Scopes (extra context attached to a group of messages) are not used by the dashboard
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // formatter builds the final text out of the message template and its values
            _store.Add(new LogEntry(DateTime.Now, logLevel, _category, formatter(state, exception), exception));
        }
    }
}

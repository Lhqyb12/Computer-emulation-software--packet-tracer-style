using System.Collections.ObjectModel;       // ObservableCollection - a list that tells the screen when it changes
using System.Diagnostics;                   // Process - information about this running program
using Avalonia.Threading;                   // Dispatcher and DispatcherTimer - running code on the UI thread
using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, SetProperty
using CommunityToolkit.Mvvm.Input;          // IRelayCommand, AsyncRelayCommand - what a button runs
using Microsoft.EntityFrameworkCore;        // AsNoTracking / ToListAsync - asynchronous DB queries
using NetSim.Server.Data;                   // AppDbContext
using NetSim.Server.Networking;             // SocketServer, LogCategories
using NetSim.Server.Services;               // SecurityLog

namespace NetSim.Server.Dashboard;

// The object the dashboard window reads its data from (its DataContext). The window never talks to
// the database or the server directly - it only shows whatever this class holds.
//
// ObservableObject (from CommunityToolkit.Mvvm, the same library the client uses) gives us SetProperty:
// it stores a new value AND tells the screen "this property changed", so the number on screen updates
public class DashboardViewModel : ObservableObject
{
    // The server's DI container - the same one the socket server gets its services from.
    // The dashboard uses it to ask for an AppDbContext, exactly like the admin seed in Program.cs
    private readonly IServiceProvider _services;

    // When this server process was started - the starting point for "Running for"
    private readonly DateTime _startedAt = Process.GetCurrentProcess().StartTime;

    // The time of every request of the last hour, oldest first. A Queue because that is exactly how
    // it is used: new times join at the back, expired ones leave from the front
    private readonly Queue<DateTime> _requestTimes = new();

    // Ticks once a second on the UI thread - keeps "Running for" and "Requests (last hour)" current
    private readonly DispatcherTimer _clock;

    // The "backing fields": the real storage behind the public properties further down.
    // Each property reads its field in "get" and changes it through SetProperty in "set"
    private int _totalUsers;                          // how many users are registered
    private int _activeToday;                         // how many of them signed in today
    private int _requestsLastHour;                    // how many requests arrived in the last 60 minutes
    private int _errorCount;                          // how many requests crashed since the server started
    private string _uptime = string.Empty;            // the "Running for" text
    private string _databaseStatus = "Checking...";   // the database status text
    // null = not checked yet, true = the last database read worked, false = it failed
    private bool? _databaseOk;
    private string? _usersError;                      // the error text above the Users table (null = no error)
    private string? _securityError;                   // the error text above the Security table (null = no error)

    // The constructor. services = the server's DI container, passed in by DashboardApp
    public DashboardViewModel(IServiceProvider services)
    {
        // Keep the container for the methods that read the database
        _services = services;

        // Connect each Refresh button to the method it runs. AsyncRelayCommand wraps an async method
        // so a button can call it
        RefreshUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
        RefreshSecurityCommand = new AsyncRelayCommand(LoadSecurityEventsAsync);

        // The address our socket server listens on. The port is read from SocketServer itself rather
        // than typed here by hand - if it ever changes there, this stays correct
        ServerAddress = $"tcp://127.0.0.1:{SocketServer.Port}";

        // Fire-and-forget: loads the tables the moment the window is created, without making the
        // constructor itself async (constructors can't be async in C#)
        _ = LoadUsersAsync();
        _ = LoadSecurityEventsAsync();

        // Connects to the server's log: Listen returns what was logged before the window opened
        // (oldest first) and from now on calls OnLogAdded for every new message
        var logStore = _services.GetRequiredService<DashboardLogStore>();

        // Show every message that already exists, one by one
        foreach (LogEntry entry in logStore.Listen(OnLogAdded))
        {
            ShowLogEntry(entry);
        }

        // Fill "Running for" right away, instead of waiting a second for the first tick
        UpdateClock();

        // DispatcherTimer (not a plain Timer): its Tick runs on the UI thread, so it may update
        // properties the screen is bound to without any Dispatcher.Post
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        // "+=" adds code to run on every tick. "(_, _) =>" = a small method whose two parameters we ignore
        _clock.Tick += (_, _) => UpdateClock();

        // Start ticking
        _clock.Start();
    }

    // ----- Overview tab -----
    // The text "tcp://127.0.0.1:9000". "{ get; }" = set once in the constructor, then only read
    public string ServerAddress { get; }

    // How long the server has been running, e.g. "2h 14m".
    // "get => _uptime" returns the field. "private set" = only this class can change it;
    // SetProperty stores the new value in the field and tells the screen to redraw this text
    public string Uptime
    {
        get => _uptime;
        private set => SetProperty(ref _uptime, value);
    }

    // "Connected (checked 18:42:10)" / "Not reachable (checked 18:42:10)"
    public string DatabaseStatus
    {
        get => _databaseStatus;
        private set => SetProperty(ref _databaseStatus, value);
    }

    // Two yes/no properties for the colour of the status text: green, red, or neither (not checked yet)
    public bool IsDatabaseOk => _databaseOk == true;
    public bool IsDatabaseDown => _databaseOk == false;

    // Both numbers are calculated in LoadUsersAsync from the Users table
    public int TotalUsers
    {
        get => _totalUsers;
        private set => SetProperty(ref _totalUsers, value);
    }

    // How many users signed in since midnight
    public int ActiveToday
    {
        get => _activeToday;
        private set => SetProperty(ref _activeToday, value);
    }

    // How many requests the server handled in the last 60 minutes
    public int RequestsLastHour
    {
        get => _requestsLastHour;
        private set => SetProperty(ref _requestsLastHour, value);
    }

    // How many requests crashed since the server was started (the Errors tab has the details)
    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    // Runs once a second
    private void UpdateClock()
    {
        // The time that passed since the server started
        TimeSpan running = DateTime.Now - _startedAt;

        // Choose the format by how long it is: "2h 14m" from an hour, "14m 5s" from a minute, otherwise "5s".
        // "(int)" drops the fraction of the hours
        Uptime = running.TotalHours >= 1 ? $"{(int)running.TotalHours}h {running.Minutes}m"
            : running.TotalMinutes >= 1 ? $"{running.Minutes}m {running.Seconds}s"
            : $"{running.Seconds}s";

        // Requests older than an hour leave the queue. They are in time order, so it is enough to
        // look at the front: once the oldest one is recent enough, all the others are too
        DateTime oneHourAgo = DateTime.Now.AddHours(-1);

        // Peek = look at the oldest time without removing it
        while (_requestTimes.Count > 0 && _requestTimes.Peek() < oneHourAgo)
        {
            // Dequeue = remove the oldest time
            _requestTimes.Dequeue();
        }

        // What is left in the queue is the number of requests of the last hour
        RequestsLastHour = _requestTimes.Count;
    }

    // Called after every database read the dashboard makes, with whether it worked.
    // The dashboard deliberately does NOT ping the database on a timer: the database is hosted in the
    // cloud and goes to sleep when nobody uses it - a ping every few seconds would keep it awake
    // around the clock for nothing. So the status means "the result of the last real read"
    private void ReportDatabase(bool ok)
    {
        // Remember the result
        _databaseOk = ok;

        // Build the status text with the current time, e.g. "Connected (checked 18:42:10)"
        DatabaseStatus = (ok ? "Connected" : "Not reachable") + $" (checked {DateTime.Now:HH:mm:ss})";

        // These two have no setter of their own, so the screen is told by hand that they changed.
        // nameof(X) = the name of X as text, checked by the compiler
        OnPropertyChanged(nameof(IsDatabaseOk));
        OnPropertyChanged(nameof(IsDatabaseDown));
    }

    // ----- Users tab (read from the database) -----
    // ObservableCollection (not a plain List): a list that tells the screen "I changed" whenever an
    // item is added or removed, so the table redraws by itself
    public ObservableCollection<UserRow> Users { get; } = new();

    // null = everything is fine. Otherwise the text shown in the red banner above the table
    public string? UsersError
    {
        get => _usersError;
        private set => SetProperty(ref _usersError, value);
    }

    // What the "Refresh" button runs
    public IRelayCommand RefreshUsersCommand { get; }

    // Reads the Users table from the database and fills the Users list and the two numbers
    private async Task LoadUsersAsync()
    {
        // Clear the error of the previous attempt
        UsersError = null;

        // Reading the database can fail, so it runs inside "try"
        try
        {
            // AppDbContext is Scoped (one per request). The dashboard is not inside any request,
            // so it opens a scope of its own, uses the context, and disposes both when the method ends
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Select asks the database only for the columns the table shows. The password hash and
            // the reset-code hash never leave the database - "HasPassword" is computed inside the
            // SQL query, so the dashboard learns only yes/no, never the hash itself.
            // AsNoTracking: we only read, so EF Core doesn't need to track these rows for changes
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)                        // sorted by id, oldest account first
                .Select(u => new                           // "new { ... }" = a small nameless object with these fields
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.CreatedAt,
                    u.LastLoginAt,
                    HasPassword = u.PasswordHash != "",    // true if the account has a password
                    HasGoogle = u.GoogleId != null,        // true if the account is linked to Google
                })
                .ToListAsync();                            // run the query and collect the results

            // Empty the table on screen before filling it again
            Users.Clear();

            // One round for every user that was read
            foreach (var u in users)
            {
                // Choose the "sign-in method" text from the two yes/no values together.
                // "_" = any other combination
                string signIn = (u.HasPassword, u.HasGoogle) switch
                {
                    (true, true) => "Password + Google",
                    (false, true) => "Google",
                    _ => "Password",
                };

                // The database stores UTC; ToLocalTime converts to this computer's clock for display.
                // The last line: a date and time if the user ever signed in, otherwise the word "Never"
                Users.Add(new UserRow(
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    signIn,
                    u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                    u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Never"));
            }

            // The number for the "Total users" card
            TotalUsers = users.Count;

            // "Today" starts at local midnight; converted to UTC to compare with the stored values
            DateTime startOfToday = DateTime.Today.ToUniversalTime();

            // Count the users whose last sign-in is after midnight
            ActiveToday = users.Count(u => u.LastLoginAt >= startOfToday);

            // The read worked - show "Connected"
            ReportDatabase(true);
        }
        // Any error while reading
        catch (Exception ex)
        {
            // For example: the database is not reachable. The window stays open and says what went wrong
            UsersError = "Could not read the Users table: " + ex.Message;

            // Show "Not reachable"
            ReportDatabase(false);
        }
    }

    // ----- Logs tab (live messages from the running server) -----
    // The rows of the Logs table, newest first
    public ObservableCollection<LogRow> Logs { get; } = new();

    // The table keeps only the newest rows, so the window stays fast after days of running
    private const int MaxLogRows = 500;

    // Called by the log store for every new message - on whatever thread the server happened to be
    // handling that request. The screen may only be touched from the UI thread, so instead of changing
    // the list here we Post the work to the UI thread, which runs it a moment later
    private void OnLogAdded(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() => ShowLogEntry(entry));
    }

    // Puts one log entry on the screen. Always runs on the UI thread.
    // Insert(0, ...) adds the row at the top, so the newest message is always the first line
    private void ShowLogEntry(LogEntry entry)
    {
        // Turn the entry into a table row and put it first
        Logs.Insert(0, ToLogRow(entry));

        // Too many rows?
        if (Logs.Count > MaxLogRows)
        {
            Logs.RemoveAt(Logs.Count - 1);   // drop the oldest (the last line)
        }

        // Every handled request is counted for "Requests (last hour)"
        if (entry.Category == LogCategories.Requests)

        {
            // Add its time to the back of the queue and update the number on screen
            _requestTimes.Enqueue(entry.Time);
            RequestsLastHour = _requestTimes.Count;
        }

        // A crash caught by MessageRouter also gets a row in the Errors tab
        if (entry.Category == LogCategories.Crashes && entry.Exception is not null)

        {
            // Put the new error first
            Errors.Insert(0, ToErrorRow(entry, entry.Exception));

            // Keep the Errors table short too
            if (Errors.Count > MaxLogRows)
            {
                Errors.RemoveAt(Errors.Count - 1);
            }

            // One more crash for the "Errors" card
            ErrorCount++;
        }

        // A security event was just saved to the database - reload the Security tab so it shows up
        // immediately, without the owner having to press Refresh
        if (entry.Category == SecurityLog.Category)
        {
            _ = LoadSecurityEventsAsync();
        }
    }

    // Turns a raw log entry into the ready-to-show text of one table row
    private static LogRow ToLogRow(LogEntry entry)
    {
        // The level as one of three words. "_" = everything else (Information, Debug, Trace)
        string level = entry.Level switch
        {
            LogLevel.Warning => "Warning",
            LogLevel.Error or LogLevel.Critical => "Error",
            _ => "Info",
        };

        // "NetSim.Requests" -> "Requests": only the last part of the category name fits the column.
        // LastIndexOf('.') finds the last dot; "[(that + 1)..]" = everything after it
        string source = entry.Category[(entry.Category.LastIndexOf('.') + 1)..];

        // When the message came with an exception, its type and text are the most useful part
        string message = entry.Exception is null
            ? entry.Message
            : $"{entry.Message} | {entry.Exception.GetType().Name}: {entry.Exception.Message}";

        // Build the row; the time is shown as hours:minutes:seconds
        return new LogRow(entry.Time.ToString("HH:mm:ss"), level, source, message);
    }

    // ----- Errors tab (requests that crashed since the server started) -----
    // The rows of the Errors table, newest first
    public ObservableCollection<ErrorRow> Errors { get; } = new();

    // Turns a crash log entry into one row of the Errors table
    private static ErrorRow ToErrorRow(LogEntry entry, Exception exception)
    {
        // The stack trace lists the chain of method calls that led to the exception, innermost first.
        // Its first line is where the exception was actually thrown - the place to start looking.
        // (The complete stack trace is printed in the terminal window by the Console logger).
        // "?." and "??" = if there is no stack trace at all, use the text "(no stack trace)"
        string location = exception.StackTrace?.Split('\n')[0].Trim() ?? "(no stack trace)";

        // MessageRouter logs a crash with the message type as its text, e.g. "login"
        return new ErrorRow(
            entry.Time.ToString("HH:mm:ss"),   // when it happened
            entry.Message,                     // which request crashed
            exception.GetType().Name,          // the kind of exception, e.g. "NullReferenceException"
            exception.Message,                 // the exception's own text
            location);                         // where it was thrown
    }

    // ----- Security tab (read from the SecurityEvents table) -----
    // The rows of the Security table, newest first
    public ObservableCollection<SecurityEventRow> SecurityEvents { get; } = new();

    // The table shows only the newest events; the database keeps all of them
    private const int MaxSecurityRows = 200;

    // null = everything is fine. Otherwise the text shown in the red banner above the table
    public string? SecurityError
    {
        get => _securityError;
        private set => SetProperty(ref _securityError, value);
    }

    // What the "Refresh" button of the Security tab runs
    public IRelayCommand RefreshSecurityCommand { get; }

    // Reads the newest security events from the database and fills the SecurityEvents list
    private async Task LoadSecurityEventsAsync()
    {
        // Clear the error of the previous attempt
        SecurityError = null;

        // Reading the database can fail, so it runs inside "try"
        try
        {
            // A scope of our own with its own database connection (same reason as in LoadUsersAsync)
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // OrderByDescending + Take: the database itself sorts newest-first and returns only the
            // first 200 rows - the server never loads the whole history into memory
            var events = await db.SecurityEvents
                .AsNoTracking()
                .OrderByDescending(e => e.OccurredAt)
                .Take(MaxSecurityRows)
                .ToListAsync();

            // Empty the table on screen before filling it again
            SecurityEvents.Clear();

            // One round for every event that was read
            foreach (var e in events)
            {
                // Unlike the Logs tab these rows can be days old, so the date is shown too.
                // "??" = show "-" where the email or the address is unknown
                SecurityEvents.Add(new SecurityEventRow(
                    e.OccurredAt.ToLocalTime().ToString("dd/MM HH:mm:ss"),
                    e.EventType,
                    e.Email ?? "-",
                    e.IpAddress ?? "-",
                    e.Details));
            }

            // The read worked - show "Connected"
            ReportDatabase(true);
        }
        // Any error while reading
        catch (Exception ex)
        {
            // The window stays open and says what went wrong
            SecurityError = "Could not read the SecurityEvents table: " + ex.Message;

            // Show "Not reachable"
            ReportDatabase(false);
        }
    }
}

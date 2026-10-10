using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Middleware;
using NetSim.Server.Services;

namespace NetSim.Server.Dashboard;

// The object the dashboard window reads its data from (its DataContext). The window never talks to
// the database or the server directly - it only shows whatever this class holds.
//
// ObservableObject (from CommunityToolkit.Mvvm, the same library the client uses) gives us SetProperty:
// it stores a new value AND tells the screen "this property changed", so the number on screen updates
public class DashboardViewModel : ObservableObject
{
    // The server's DI container - the same one the controllers get their services from.
    // The dashboard uses it to ask for an AppDbContext, exactly like the admin seed in Program.cs
    private readonly IServiceProvider _services;

    // When this server process was started - the starting point for "Running for"
    private readonly DateTime _startedAt = Process.GetCurrentProcess().StartTime;

    // The time of every request of the last hour, oldest first. A Queue because that is exactly how
    // it is used: new times join at the back, expired ones leave from the front
    private readonly Queue<DateTime> _requestTimes = new();

    // Ticks once a second on the UI thread - keeps "Running for" and "Requests (last hour)" current
    private readonly DispatcherTimer _clock;

    private int _totalUsers;
    private int _activeToday;
    private int _requestsLastHour;
    private int _errorCount;
    private string _uptime = string.Empty;
    private string _databaseStatus = "Checking...";
    // null = not checked yet, true = the last database read worked, false = it failed
    private bool? _databaseOk;
    private string? _usersError;
    private string? _securityError;

    public DashboardViewModel(IServiceProvider services)
    {
        _services = services;
        RefreshUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
        RefreshSecurityCommand = new AsyncRelayCommand(LoadSecurityEventsAsync);

        // The address Kestrel is really listening on, asked from the server itself rather than typed
        // here by hand - if the port in launchSettings.json ever changes, this stays correct
        ServerAddress = _services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "(unknown)";

        // Fire-and-forget: loads the tables the moment the window is created, without making the
        // constructor itself async (constructors can't be async in C#)
        _ = LoadUsersAsync();
        _ = LoadSecurityEventsAsync();

        // Connects to the server's log: Listen returns what was logged before the window opened
        // (oldest first) and from now on calls OnLogAdded for every new message
        var logStore = _services.GetRequiredService<DashboardLogStore>();
        foreach (LogEntry entry in logStore.Listen(OnLogAdded))
        {
            ShowLogEntry(entry);
        }

        // DispatcherTimer (not a plain Timer): its Tick runs on the UI thread, so it may update
        // properties the screen is bound to without any Dispatcher.Post
        UpdateClock();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();
    }

    // ----- Overview tab -----
    public string ServerAddress { get; }

    // How long the server has been running, e.g. "2h 14m"
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
        TimeSpan running = DateTime.Now - _startedAt;
        Uptime = running.TotalHours >= 1 ? $"{(int)running.TotalHours}h {running.Minutes}m"
            : running.TotalMinutes >= 1 ? $"{running.Minutes}m {running.Seconds}s"
            : $"{running.Seconds}s";

        // Requests older than an hour leave the queue. They are in time order, so it is enough to
        // look at the front: once the oldest one is recent enough, all the others are too
        DateTime oneHourAgo = DateTime.Now.AddHours(-1);
        while (_requestTimes.Count > 0 && _requestTimes.Peek() < oneHourAgo)
        {
            _requestTimes.Dequeue();
        }
        RequestsLastHour = _requestTimes.Count;
    }

    // Called after every database read the dashboard makes, with whether it worked.
    // The dashboard deliberately does NOT ping the database on a timer: the database is hosted in the
    // cloud and goes to sleep when nobody uses it - a ping every few seconds would keep it awake
    // around the clock for nothing. So the status means "the result of the last real read"
    private void ReportDatabase(bool ok)
    {
        _databaseOk = ok;
        DatabaseStatus = (ok ? "Connected" : "Not reachable") + $" (checked {DateTime.Now:HH:mm:ss})";

        // These two have no setter of their own, so the screen is told by hand that they changed
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

    private async Task LoadUsersAsync()
    {
        UsersError = null;
        try
        {
            // AppDbContext is Scoped (one per HTTP request). The dashboard is not inside any request,
            // so it opens a scope of its own, uses the context, and disposes both when the method ends
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Select asks the database only for the columns the table shows. The password hash and
            // the reset-code hash never leave the database - "HasPassword" is computed inside the
            // SQL query, so the dashboard learns only yes/no, never the hash itself.
            // AsNoTracking: we only read, so EF Core doesn't need to track these rows for changes
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.CreatedAt,
                    u.LastLoginAt,
                    HasPassword = u.PasswordHash != "",
                    HasGoogle = u.GoogleId != null,
                })
                .ToListAsync();

            Users.Clear();
            foreach (var u in users)
            {
                string signIn = (u.HasPassword, u.HasGoogle) switch
                {
                    (true, true) => "Password + Google",
                    (false, true) => "Google",
                    _ => "Password",
                };

                // The database stores UTC; ToLocalTime converts to this computer's clock for display
                Users.Add(new UserRow(
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    signIn,
                    u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                    u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Never"));
            }

            TotalUsers = users.Count;

            // "Today" starts at local midnight; converted to UTC to compare with the stored values
            DateTime startOfToday = DateTime.Today.ToUniversalTime();
            ActiveToday = users.Count(u => u.LastLoginAt >= startOfToday);

            ReportDatabase(true);
        }
        catch (Exception ex)
        {
            // For example: the database is not reachable. The window stays open and says what went wrong
            UsersError = "Could not read the Users table: " + ex.Message;
            ReportDatabase(false);
        }
    }

    // ----- Logs tab (live messages from the running server) -----
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
        Logs.Insert(0, ToLogRow(entry));
        if (Logs.Count > MaxLogRows)
        {
            Logs.RemoveAt(Logs.Count - 1);   // drop the oldest (the last line)
        }

        // Every handled request is counted for "Requests (last hour)"
        if (entry.Category == RequestLoggingMiddleware.RequestCategory)

        {
            _requestTimes.Enqueue(entry.Time);
            RequestsLastHour = _requestTimes.Count;
        }

        // A crash caught by RequestLoggingMiddleware also gets a row in the Errors tab
        if (entry.Category == RequestLoggingMiddleware.CrashCategory && entry.Exception is not null)

        {
            Errors.Insert(0, ToErrorRow(entry, entry.Exception));
            if (Errors.Count > MaxLogRows)
            {
                Errors.RemoveAt(Errors.Count - 1);
            }
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
        string level = entry.Level switch
        {
            LogLevel.Warning => "Warning",
            LogLevel.Error or LogLevel.Critical => "Error",
            _ => "Info",
        };

        // "NetSim.Requests" -> "Requests": only the last part of the category name fits the column
        string source = entry.Category[(entry.Category.LastIndexOf('.') + 1)..];

        // When the message came with an exception, its type and text are the most useful part
        string message = entry.Exception is null
            ? entry.Message
            : $"{entry.Message} | {entry.Exception.GetType().Name}: {entry.Exception.Message}";

        return new LogRow(entry.Time.ToString("HH:mm:ss"), level, source, message);
    }

    // ----- Errors tab (requests that crashed since the server started) -----
    public ObservableCollection<ErrorRow> Errors { get; } = new();

    private static ErrorRow ToErrorRow(LogEntry entry, Exception exception)
    {
        // The stack trace lists the chain of method calls that led to the exception, innermost first.
        // Its first line is where the exception was actually thrown - the place to start looking.
        // (The complete stack trace is printed in the terminal window by the Console logger)
        string location = exception.StackTrace?.Split('\n')[0].Trim() ?? "(no stack trace)";

        // The middleware logs crashes with the message "{Method} {Path}", e.g. "POST /api/auth/login"
        return new ErrorRow(
            entry.Time.ToString("HH:mm:ss"),
            entry.Message,
            exception.GetType().Name,
            exception.Message,
            location);
    }

    // ----- Security tab (read from the SecurityEvents table) -----
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

    private async Task LoadSecurityEventsAsync()
    {
        SecurityError = null;
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // OrderByDescending + Take: the database itself sorts newest-first and returns only the
            // first 200 rows - the server never loads the whole history into memory
            var events = await db.SecurityEvents
                .AsNoTracking()
                .OrderByDescending(e => e.OccurredAt)
                .Take(MaxSecurityRows)
                .ToListAsync();

            SecurityEvents.Clear();
            foreach (var e in events)
            {
                // Unlike the Logs tab these rows can be days old, so the date is shown too
                SecurityEvents.Add(new SecurityEventRow(
                    e.OccurredAt.ToLocalTime().ToString("dd/MM HH:mm:ss"),
                    e.EventType,
                    e.Email ?? "-",
                    e.IpAddress ?? "-",
                    e.Details));
            }

            ReportDatabase(true);
        }
        catch (Exception ex)
        {
            SecurityError = "Could not read the SecurityEvents table: " + ex.Message;
            ReportDatabase(false);
        }
    }
}

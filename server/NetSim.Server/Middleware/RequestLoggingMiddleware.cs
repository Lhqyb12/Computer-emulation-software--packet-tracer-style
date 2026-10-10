using System.Diagnostics;

namespace NetSim.Server.Middleware;

// A middleware is a station every HTTP request passes through on its way to the controller, and again
// on its way back with the response. This one does two jobs:
//  1. writes one log line per request: what was asked, what the answer was, how long it took and
//     where it came from
//  2. catches crashes: an exception nobody handled, anywhere further down the pipeline
//
// Deliberately NOT logged: the request body and the query string. The body of /api/auth/login contains
// the password - writing it to a log would leak it to anyone who can read the log
public class RequestLoggingMiddleware
{
    // The category crashes are logged under. A constant, so the dashboard can recognise these messages
    // by the same name without the text being typed twice (a typo in one place would silently break it)
    public const string CrashCategory = "NetSim.Crashes";
    
    // The category of the one-line-per-request messages - the dashboard counts these for
    // "Requests (last hour)", and shows the last part ("Requests") in its SOURCE column
    public const string RequestCategory = "NetSim.Requests";


    // The rest of the pipeline (the next middleware, and eventually the controller)
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly ILogger _crashLogger;

    public RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger(RequestCategory);

        _crashLogger = loggerFactory.CreateLogger(CrashCategory);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Hands the request on and waits here until the response is ready
            await _next(context);
        }
        catch (Exception ex)
        {
            // Something further down threw and nobody caught it - a bug, or something the code didn't
            // expect (database down, mail server refusing...). Record it with the full exception:
            // the Console logger prints the whole stack trace, the dashboard shows it in the Errors tab
            _crashLogger.LogError(ex, "{Method} {Path}", context.Request.Method, context.Request.Path);

            // The client gets a short, generic answer. The exception text and the stack trace stay on
            // the server: they reveal file names, table names and how the code works - a gift to an attacker.
            // HasStarted: if part of the response was already sent, it's too late to change it
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
            }
        }

        stopwatch.Stop();
        int status = context.Response.StatusCode;

        // The status code decides the severity: 5xx = the server failed, 4xx = the request was refused
        // (wrong password, no permission, not found...), anything else = fine
        LogLevel level = status >= 500 ? LogLevel.Error
            : status >= 400 ? LogLevel.Warning
            : LogLevel.Information;

        _logger.Log(level, "{Method} {Path} -> {Status} ({Elapsed} ms) from {Ip}",
            context.Request.Method,
            context.Request.Path,
            status,
            stopwatch.ElapsedMilliseconds,
            context.Connection.RemoteIpAddress);
    }
}

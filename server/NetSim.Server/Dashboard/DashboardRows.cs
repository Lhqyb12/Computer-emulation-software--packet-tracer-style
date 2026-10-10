namespace NetSim.Server.Dashboard;

// The "row" types the dashboard tables display. Each one is a record: a short way to declare a class
// that only carries data - the compiler generates the constructor and the read-only properties from
// the list in the parentheses. They hold ready-to-show text (dates already formatted), so the
// .axaml file only has to print them

// One line in the Users table. There is deliberately no password hash here - it is never displayed
public record UserRow(
    int Id,
    string Username,
    string Email,
    string Role,
    string SignInMethod,
    string CreatedAt,
    string LastLogin);

// One line in the Logs table - something the server did
public record LogRow(string Time, string Level, string Source, string Message)
{
    // Used by the .axaml file to pick the colour of the Level text
    public bool IsWarning => Level == "Warning";
    public bool IsError => Level == "Error";
}

// One line in the Errors table - a request that crashed with an unhandled exception.
// Location is the first line of the stack trace: the method (and file/line) where it was thrown
public record ErrorRow(string Time, string Request, string ExceptionType, string Message, string Location);


// One line in the Security table - an event worth the owner's attention
public record SecurityEventRow(string Time, string EventType, string Email, string IpAddress, string Details);

namespace NetSim.Server.Dtos;

// The answer to a "get-users" message: the usual success/message pair, plus the list itself
public class UsersResponse
{
    // true = the list was read
    public bool Success { get; set; }

    // A human-readable message (used when something went wrong)
    public string Message { get; set; } = string.Empty;

    // One item per user. "= new()" = an empty list by default, so it is never null
    public List<UserSummaryDto> Users { get; set; } = new();
}

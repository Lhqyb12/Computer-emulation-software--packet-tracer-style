namespace NetSim.Server.Dtos;

// The answer to a "get-users" message: the usual success/message pair, plus the list itself
public class UsersResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<UserSummaryDto> Users { get; set; } = new();
}

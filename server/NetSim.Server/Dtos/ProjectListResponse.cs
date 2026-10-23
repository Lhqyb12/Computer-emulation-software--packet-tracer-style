namespace NetSim.Server.Dtos;

// The server's answer to "get-projects": the projects of the signed-in user only.
// In the NetworkSimulator project this was a bare list, because HTTP's status code (200 / 401) already
// said whether it worked. Our socket protocol has no status codes, so - like every other answer here -
// success and the error message travel inside the message itself
public class ProjectListResponse
{
    // true = here is the list, false = refused (e.g. no valid token)
    public bool Success { get; set; }

    // Why it was refused. Empty on success
    public string Message { get; set; } = string.Empty;

    // The user's projects, most recently modified first. Empty (not null) when they have none yet
    public List<ProjectDto> Projects { get; set; } = new();
}

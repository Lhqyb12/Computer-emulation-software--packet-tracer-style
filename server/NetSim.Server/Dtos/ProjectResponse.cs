namespace NetSim.Server.Dtos;

// The server's answer to "save-project" and "delete-project".
// Based on ProjectResponse from the NetworkSimulator project, which deliberately mirrors AuthResponse's
// shape - so every project answer reports success/failure the same way the auth answers already do
public class ProjectResponse
{
    // true = the operation succeeded, false = it was refused
    public bool Success { get; set; }

    // A human-readable message, e.g. "A project named 'Lab 1' already exists." Empty on success
    public string Message { get; set; } = string.Empty;

    // The project as it is now stored, after a successful save. null for a delete, or when the save failed
    public ProjectDto? Project { get; set; }
}

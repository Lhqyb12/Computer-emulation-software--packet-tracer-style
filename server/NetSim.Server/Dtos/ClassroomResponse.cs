namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The server's answer to every class action that changes something: create, join, leave,
// remove a student, delete. The same shape as ProjectResponse and AuthResponse
public class ClassroomResponse
{
    // true = the action succeeded, false = it was refused
    public bool Success { get; set; }

    // The text to show, e.g. "No class has this code. Check it with your teacher." Empty on success
    public string Message { get; set; } = string.Empty;

    // The class after a successful create / join (so the client can show its name and code).
    // null for leave / remove / delete, and when the action failed
    public ClassroomDto? Classroom { get; set; }
}

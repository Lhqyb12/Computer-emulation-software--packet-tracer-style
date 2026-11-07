namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The server's answer to "get-classrooms": both halves of the Classes screen in one answer
public class ClassroomListResponse
{
    // true = here are the lists, false = refused (e.g. no valid token)
    public bool Success { get; set; }

    // Why it was refused. Empty on success
    public string Message { get; set; } = string.Empty;

    // The classes the signed-in user teaches, newest first. Empty (not null) when there are none
    public List<ClassroomDto> Teaching { get; set; } = new();

    // The classes the signed-in user joined as a student, newest first. Empty (not null) when there are none
    public List<ClassroomDto> Joined { get; set; } = new();
}

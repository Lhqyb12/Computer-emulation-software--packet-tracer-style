namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "data" part of the class messages. None of them says who is asking - the server always takes
// that from the verified token, so a user can't act in someone else's name

// The data of "create-classroom": the name of the new class
public class CreateClassroomRequest
{
    public string Name { get; set; } = string.Empty;   // the class name the teacher typed
}

// The data of "join-classroom": the code the student typed
public class JoinClassroomRequest
{
    public string JoinCode { get; set; } = string.Empty;   // e.g. "K7QMTX" (small letters and spaces are fine)
}

// The data of "leave-classroom" and "delete-classroom": which class
public class ClassroomIdRequest
{
    public Guid Id { get; set; }   // the class's id
}

// The data of "remove-student": which student, from which class
public class RemoveStudentRequest
{
    public Guid ClassroomId { get; set; }                     // the class
    public string StudentEmail { get; set; } = string.Empty;  // the student to remove
}

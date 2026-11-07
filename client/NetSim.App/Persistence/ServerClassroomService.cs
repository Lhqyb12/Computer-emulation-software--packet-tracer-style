using System.Collections.Generic;       // IReadOnlyList, List
using System.Linq;                      // Select, ToList
using System.Threading.Tasks;           // Task
using NetSim.App.Services;              // AuthApiClient, ClassroomDto, ClassroomResult
using NetSim.Application.Classrooms;    // Classroom, MyClassrooms
using NetSim.Application.Common;        // OperationResult, OperationErrorType
using NetSim.Application.Services;      // IClassroomService
using NetSim.Application.State;         // ICurrentUser - holds the signed-in user's token
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The real IClassroomService: every method asks the server, over the encrypted socket, through AuthApiClient.
// Unlike projects, the class RULES run on the server (who is the teacher, is the code right...), because
// they involve other users - so this replaces the whole service, not just the storage.
// The Classes screen doesn't know the difference: it only knows IClassroomService.
// Failures (including "the server is down") come back as a Failure result with the server's message
public sealed class ServerClassroomService : IClassroomService
{
    // Our connection to the server - the same way the login screen creates its own
    private readonly AuthApiClient _api = new();

    // Who is signed in. Its Token goes with every request, so the server knows who is asking
    private readonly ICurrentUser _currentUser;

    // Purpose: Builds the service. Dependency injection passes in the signed-in user.
    // Input:   currentUser - the user of this sign-in (filled by MainWindowViewModel).
    // Output:  none (a constructor).
    public ServerClassroomService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;   // keep the signed-in user, to read its token later
    }

    // Purpose: Reads both halves of the Classes screen from the server.
    // Input:   nothing.
    // Output:  a Task with Success + the classes I teach and the classes I joined, or Failure with the reason.
    public async Task<OperationResult<MyClassrooms>> GetMyClassroomsAsync()
    {
        // Ask the server, with the token, and wait for the answer
        var result = await _api.GetClassroomsAsync(_currentUser.Token);

        // Refused, or the server is down - pass the reason on
        if (!result.Success)
            return OperationResult<MyClassrooms>.Failure(OperationErrorType.InvalidState, result.Message);

        // Turn every ClassroomDto into a Classroom of the Application layer, for both lists
        var teaching = result.Teaching.Select(ToClassroom).ToList();   // the classes I teach
        var joined = result.Joined.Select(ToClassroom).ToList();       // the classes I joined

        // Success, with both lists
        return OperationResult<MyClassrooms>.Success(new MyClassrooms(teaching, joined));
    }

    // Purpose: Asks the server to create a class taught by the signed-in user.
    // Input:   name - the class name.
    // Output:  a Task with Success + the new class (with its code), or Failure with the reason.
    public async Task<OperationResult<Classroom>> CreateClassroomAsync(string name) =>
        ToClassroomResult(await _api.CreateClassroomAsync(name, _currentUser.Token));   // send, wait, and turn the answer into a result

    // Purpose: Asks the server to add the signed-in user as a student to the class with this code.
    // Input:   joinCode - the code the student typed.
    // Output:  a Task with Success + the class, or Failure with the reason.
    public async Task<OperationResult<Classroom>> JoinClassroomAsync(string joinCode) =>
        ToClassroomResult(await _api.JoinClassroomAsync(joinCode, _currentUser.Token));   // send, wait, turn into a result

    // Purpose: Asks the server to take the signed-in user out of a class.
    // Input:   classroomId - the class.
    // Output:  a Task with Success, or Failure with the reason.
    // classroomId.Value = the Guid inside the EntityId
    public async Task<OperationResult> LeaveClassroomAsync(EntityId classroomId) =>
        ToResult(await _api.LeaveClassroomAsync(classroomId.Value, _currentUser.Token));   // send, wait, turn into a result

    // Purpose: Asks the server to remove a student from one of my classes.
    // Input:   classroomId - the class; studentEmail - the student.
    // Output:  a Task with Success, or Failure with the reason (e.g. I am not its teacher).
    public async Task<OperationResult> RemoveStudentAsync(EntityId classroomId, string studentEmail) =>
        ToResult(await _api.RemoveStudentAsync(classroomId.Value, studentEmail, _currentUser.Token));   // send, wait, turn into a result

    // Purpose: Asks the server to delete one of my classes.
    // Input:   classroomId - the class.
    // Output:  a Task with Success, or Failure with the reason.
    public async Task<OperationResult> DeleteClassroomAsync(EntityId classroomId) =>
        ToResult(await _api.DeleteClassroomAsync(classroomId.Value, _currentUser.Token));   // send, wait, turn into a result

    // Purpose: Turns the server's answer to create / join into a result with a Classroom.
    // Input:   result - the server's answer.
    // Output:  Success + the class, or Failure with the server's message.
    private static OperationResult<Classroom> ToClassroomResult(ClassroomResult result)
    {
        // Refused (or no class came back) - pass the reason on. InvalidState = a general "could not do it now"
        if (!result.Success || result.Classroom is null)
            return OperationResult<Classroom>.Failure(OperationErrorType.InvalidState, result.Message);

        // Success, with the class turned into a Classroom
        return OperationResult<Classroom>.Success(ToClassroom(result.Classroom));
    }

    // Purpose: Turns the server's answer to leave / remove / delete into a result without a value.
    // Input:   result - the server's answer.
    // Output:  Success, or Failure with the server's message.
    private static OperationResult ToResult(ClassroomResult result) =>
        result.Success
            ? OperationResult.Success()                                             // it worked
            : OperationResult.Failure(OperationErrorType.InvalidState, result.Message);   // refused - pass the reason on

    // Purpose: Turns one class from the server into a Classroom of the Application layer.
    // Input:   dto - one class as it came over the socket.
    // Output:  a Classroom with the same fields and the students the server sent.
    private static Classroom ToClassroom(ClassroomDto dto)
    {
        // Build the class. EntityId.FromValue = rebuild the id it already has; StudentCount = the server's number
        var classroom = new Classroom(
            EntityId.FromValue(dto.Id),   // the existing id
            dto.Name,                     // the name
            dto.JoinCode,                 // the code (empty when I am a student)
            dto.TeacherEmail,             // who teaches it
            dto.CreatedAtUtc,             // when it was created
            dto.StudentCount);            // how many students it has

        // Add every student the server sent (all of them for my classes, only me for joined ones)
        foreach (var student in dto.Students)
            classroom.AddStudent(student.Email, student.JoinedAtUtc);

        // The finished class
        return classroom;
    }
}

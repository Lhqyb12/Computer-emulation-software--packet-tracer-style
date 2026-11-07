using System.Threading.Tasks;          // Task - every method may have to wait for the server
using NetSim.Application.Classrooms;  // Classroom, MyClassrooms
using NetSim.Application.Common;      // OperationResult
using NetSim.Core.Common;             // EntityId

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Everything a user can do with classes. The screens only talk to this interface - never to the
// storage directly - exactly like IProjectService for projects.
// Every method works for the signed-in user, so no method takes "who is asking": a user can never act
// in another user's name. Every method is asynchronous, because the real one (ServerClassroomService in
// NetSim.App) has to wait for the server. Failures - including "the server is down" - come back as a
// Failure result with a message, never as an exception
public interface IClassroomService
{
    // Purpose: Reads both halves of the Classes screen.
    // Input:   nothing.
    // Output:  a Task with Success + the classes I teach and the classes I joined (newest first), or Failure.
    Task<OperationResult<MyClassrooms>> GetMyClassroomsAsync();

    // Purpose: Creates a new class with a fresh join code; the signed-in user is its teacher.
    // Input:   name - the class name.
    // Output:  a Task with Success + the class (with its code), or Failure (e.g. blank / too long name).
    Task<OperationResult<Classroom>> CreateClassroomAsync(string name);

    // Purpose: Joins the class that has this code, as a student.
    // Input:   joinCode - the code the student typed.
    // Output:  a Task with Success + the class, or Failure (badly formed code, no such class,
    //          already a member, or the teacher of it).
    Task<OperationResult<Classroom>> JoinClassroomAsync(string joinCode);

    // Purpose: The signed-in student leaves a class.
    // Input:   classroomId - the class.
    // Output:  a Task with Success, or Failure (not a student there).
    Task<OperationResult> LeaveClassroomAsync(EntityId classroomId);

    // Purpose: The teacher removes one student from their class.
    // Input:   classroomId - the class; studentEmail - the student to remove.
    // Output:  a Task with Success, or Failure (not the teacher of this class / no such class or student).
    Task<OperationResult> RemoveStudentAsync(EntityId classroomId, string studentEmail);

    // Purpose: The teacher deletes their whole class.
    // Input:   classroomId - the class.
    // Output:  a Task with Success, or Failure (no such class / not its teacher).
    Task<OperationResult> DeleteClassroomAsync(EntityId classroomId);
}

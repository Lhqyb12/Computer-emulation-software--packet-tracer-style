using System.Collections.Generic;     // IReadOnlyList
using NetSim.Application.Classrooms;  // Classroom
using NetSim.Application.Common;      // OperationResult
using NetSim.Core.Common;             // EntityId

namespace NetSim.Application.Services;

// Everything a user can do with classes. The screens only talk to this interface - never to the
// repository directly - exactly like IProjectService for projects.
// Every method works for the signed-in user (ICurrentUser), so no method takes "who is asking":
// a user can never act in another user's name
public interface IClassroomService
{
    // The classes the signed-in user created (teaches), newest first
    IReadOnlyList<Classroom> GetTeachingClassrooms();

    // The classes the signed-in user joined as a student, newest first
    IReadOnlyList<Classroom> GetJoinedClassrooms();

    // Creates a new class with a fresh join code; the signed-in user is its teacher.
    // Fails with ValidationError for a blank / too long name
    OperationResult<Classroom> CreateClassroom(string name);

    // Joins the class that has this code, as a student. Fails with ValidationError for a badly
    // formed code, NotFound if no class has it, Conflict if already a member or the teacher of it
    OperationResult<Classroom> JoinClassroom(string joinCode);

    // The signed-in student leaves a class
    OperationResult LeaveClassroom(EntityId classroomId);

    // The teacher removes one student from their class. Fails with InvalidState if the signed-in
    // user is not the teacher of this class
    OperationResult RemoveStudent(EntityId classroomId, string studentEmail);

    // The teacher deletes their whole class
    OperationResult DeleteClassroom(EntityId classroomId);
}

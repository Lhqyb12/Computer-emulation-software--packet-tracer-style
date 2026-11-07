using System;                             // DateTime
using System.Collections.Generic;         // IReadOnlyList
using System.Linq;                        // Where, OrderByDescending, ToList, All
using System.Security.Cryptography;       // RandomNumberGenerator - secure random numbers for the join code
using System.Threading.Tasks;             // Task, Task.FromResult
using NetSim.Application.Classrooms;      // Classroom
using NetSim.Application.Common;          // OperationResult, OperationErrorType
using NetSim.Application.Persistence;     // IClassroomRepository
using NetSim.Application.State;           // ICurrentUser
using NetSim.Core.Common;                 // EntityId

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of classes: who may create, join, leave, remove and delete - working IN MEMORY, through
// IClassroomRepository. The real app now uses ServerClassroomService (NetSim.App), where the server
// keeps the rules and the classes. This class is kept for testing the screen without a server
public sealed class ClassroomService : IClassroomService
{
    // The longest class name allowed - enough for a real name, short enough for the screen
    private const int MaxNameLength = 60;

    // How many characters a join code has
    public const int JoinCodeLength = 6;

    // The characters a join code is made of. 0/O and 1/I/L are left out on purpose: they look alike,
    // and a student copying the code from the board would type the wrong one
    private const string JoinCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly IClassroomRepository _repository;   // where the classes are stored
    private readonly ICurrentUser _currentUser;          // who is signed in

    // Purpose: Builds the service. Dependency injection passes in both objects.
    // Input:   repository - the class storage; currentUser - the signed-in user.
    // Output:  none (a constructor).
    public ClassroomService(IClassroomRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;   // keep the class storage
        _currentUser = currentUser;   // keep the signed-in user
    }

    // Purpose: Returns the classes the signed-in user teaches.
    // Input:   nothing.
    // Output:  the classes, newest first.
    public IReadOnlyList<Classroom> GetTeachingClassrooms() =>
        _repository.GetAll()   // all the classes in the storage
            .Where(c => c.IsTeacher(_currentUser.Email))   // keep only classes I teach
            .OrderByDescending(c => c.CreatedAtUtc)   // newest first
            .ToList();   // collect them into a list

    // Purpose: Returns the classes the signed-in user joined as a student.
    // Input:   nothing.
    // Output:  the classes, newest first.
    public IReadOnlyList<Classroom> GetJoinedClassrooms() =>
        _repository.GetAll()   // all the classes in the storage
            .Where(c => c.HasStudent(_currentUser.Email))   // keep only classes I joined as a student
            .OrderByDescending(c => c.CreatedAtUtc)   // newest first
            .ToList();   // collect them into a list

    // Purpose: Creates a class taught by the signed-in user.
    // Input:   name - the class name the teacher typed.
    // Output:  Success with the new class, or Failure with the reason.
    public OperationResult<Classroom> CreateClassroom(string name)
    {
        // An empty name, or only spaces
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, "Class name cannot be empty.");   // fail: empty name

        // Trim = remove the spaces at both ends
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)   // longer than 60 characters?
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, $"Class name cannot exceed {MaxNameLength} characters.");   // fail: too long

        // A new class: a new id, a new code, and the signed-in user as its teacher
        var classroom = new Classroom(EntityId.New(), trimmed, NewUniqueJoinCode(), _currentUser.Email, DateTime.UtcNow);

        // Store it
        _repository.Save(classroom);   // store the class (new or changed)

        return OperationResult<Classroom>.Success(classroom);   // success, with the class
    }

    // Purpose: Adds the signed-in user as a student to the class that has this code.
    // Input:   joinCode - the code the student typed.
    // Output:  Success with the class, or Failure with the reason.
    public OperationResult<Classroom> JoinClassroom(string joinCode)
    {
        // Students may type the code in small letters or with spaces around it - accept that.
        // "??" = if joinCode is null, use an empty string instead
        var code = (joinCode ?? string.Empty).Trim().ToUpperInvariant();

        // Check the format before searching: exactly 6 characters, all from the alphabet
        if (code.Length != JoinCodeLength || !code.All(ch => JoinCodeAlphabet.Contains(ch)))
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, $"A join code is {JoinCodeLength} letters and digits.");   // fail: badly formed code

        // Find the class
        var classroom = _repository.GetByJoinCode(code);   // the class with this code, or null
        if (classroom is null)   // no such class
            return OperationResult<Classroom>.Failure(OperationErrorType.NotFound, "No class has this code. Check it with your teacher.");   // fail: no class has this code

        // A teacher can't be a student in their own class
        if (classroom.IsTeacher(_currentUser.Email))
            return OperationResult<Classroom>.Failure(OperationErrorType.Conflict, "You are the teacher of this class.");   // fail: I teach it

        // Already joined before
        if (classroom.HasStudent(_currentUser.Email))
            return OperationResult<Classroom>.Failure(OperationErrorType.Conflict, $"You already joined '{classroom.Name}'.");   // fail: already a student

        // Add the student and store the change
        classroom.AddStudent(_currentUser.Email, DateTime.UtcNow);   // add me as a student, joined now
        _repository.Save(classroom);   // store the class (new or changed)

        return OperationResult<Classroom>.Success(classroom);   // success, with the class
    }

    // Purpose: Removes the signed-in user from a class they joined.
    // Input:   classroomId - the class to leave.
    // Output:  Success, or Failure with the reason.
    public OperationResult LeaveClassroom(EntityId classroomId)
    {
        var classroom = _repository.GetById(classroomId);   // find the class by its id, or null
        if (classroom is null)   // no such class
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");   // fail: no such class

        // RemoveStudent returns false if the user was not a student there
        if (!classroom.RemoveStudent(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "You are not a student in this class.");   // fail: I am not in it

        _repository.Save(classroom);   // store the class (new or changed)
        return OperationResult.Success();   // success
    }

    // Purpose: The teacher removes one student from their class.
    // Input:   classroomId - the class; studentEmail - the student to remove.
    // Output:  Success, or Failure with the reason.
    public OperationResult RemoveStudent(EntityId classroomId, string studentEmail)
    {
        var classroom = _repository.GetById(classroomId);   // find the class by its id, or null
        if (classroom is null)   // no such class
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");   // fail: no such class

        // Only the teacher of THIS class may remove its students
        if (!classroom.IsTeacher(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "Only the teacher of this class can remove students.");   // fail: I am not its teacher

        if (!classroom.RemoveStudent(studentEmail))   // take the student out; false = they were not in the class
            return OperationResult.Failure(OperationErrorType.NotFound, "This student is not in the class.");   // fail: no such student here

        _repository.Save(classroom);   // store the class (new or changed)
        return OperationResult.Success();   // success
    }

    // Purpose: The teacher deletes their class (its students simply stop being in it).
    // Input:   classroomId - the class to delete.
    // Output:  Success, or Failure with the reason.
    public OperationResult DeleteClassroom(EntityId classroomId)
    {
        var classroom = _repository.GetById(classroomId);   // find the class by its id, or null
        if (classroom is null)   // no such class
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");   // fail: no such class

        // Only the teacher of this class may delete it
        if (!classroom.IsTeacher(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "Only the teacher of this class can delete it.");   // fail: I am not its teacher

        _repository.Delete(classroomId);   // remove the class from the storage
        return OperationResult.Success();   // success
    }

    // Purpose: Makes a random join code that no other class uses yet.
    // Input:   nothing.
    // Output:  a 6-character code, e.g. "K7QMTX".
    // RandomNumberGenerator (and not new Random()) because the code is the only thing that lets
    // someone into a class - it must not be predictable. In stage 1 the client makes the code;
    // in the server stage the server will make it instead
    private string NewUniqueJoinCode()
    {
        while (true)   // repeat until a free code is found (the return below leaves the loop)
        {
            // Pick 6 random characters from the alphabet
            var chars = new char[JoinCodeLength];   // an empty array of 6 characters
            for (int i = 0; i < chars.Length; i++)   // for each of the 6 places (0 to 5)
                chars[i] = JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)];   // a secure random number 0..30 picks one character of the alphabet

            var code = new string(chars);   // the 6 characters as one text

            // Almost always free on the first try; if another class already has it, try again
            if (_repository.GetByJoinCode(code) is null)   // no class uses this code yet?
                return code;   // then it is ours
        }
    }

    // ===== The asynchronous methods IClassroomService asks for =====
    // Nothing here has to wait (everything is in memory), so each answer is wrapped in a finished Task.
    // Task.FromResult(x) = "a Task whose result is x, right now"

    // Purpose: Returns both halves of the Classes screen.
    // Input:   nothing.
    // Output:  a finished Task with Success + the classes I teach and the classes I joined.
    public Task<OperationResult<MyClassrooms>> GetMyClassroomsAsync() =>
        Task.FromResult(OperationResult<MyClassrooms>.Success(new MyClassrooms(GetTeachingClassrooms(), GetJoinedClassrooms())));

    // Purpose: The asynchronous form of CreateClassroom.
    // Input:   name - the class name.
    // Output:  a finished Task with CreateClassroom's answer.
    public Task<OperationResult<Classroom>> CreateClassroomAsync(string name) => Task.FromResult(CreateClassroom(name));

    // Purpose: The asynchronous form of JoinClassroom.
    // Input:   joinCode - the code the student typed.
    // Output:  a finished Task with JoinClassroom's answer.
    public Task<OperationResult<Classroom>> JoinClassroomAsync(string joinCode) => Task.FromResult(JoinClassroom(joinCode));

    // Purpose: The asynchronous form of LeaveClassroom.
    // Input:   classroomId - the class.
    // Output:  a finished Task with LeaveClassroom's answer.
    public Task<OperationResult> LeaveClassroomAsync(EntityId classroomId) => Task.FromResult(LeaveClassroom(classroomId));

    // Purpose: The asynchronous form of RemoveStudent.
    // Input:   classroomId - the class; studentEmail - the student.
    // Output:  a finished Task with RemoveStudent's answer.
    public Task<OperationResult> RemoveStudentAsync(EntityId classroomId, string studentEmail) => Task.FromResult(RemoveStudent(classroomId, studentEmail));

    // Purpose: The asynchronous form of DeleteClassroom.
    // Input:   classroomId - the class.
    // Output:  a finished Task with DeleteClassroom's answer.
    public Task<OperationResult> DeleteClassroomAsync(EntityId classroomId) => Task.FromResult(DeleteClassroom(classroomId));
}

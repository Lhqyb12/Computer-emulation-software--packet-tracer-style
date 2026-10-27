using System;                             // DateTime
using System.Collections.Generic;         // IReadOnlyList
using System.Linq;                        // Where, OrderByDescending, ToList, All
using System.Security.Cryptography;       // RandomNumberGenerator - secure random numbers for the join code
using NetSim.Application.Classrooms;      // Classroom
using NetSim.Application.Common;          // OperationResult, OperationErrorType
using NetSim.Application.Persistence;     // IClassroomRepository
using NetSim.Application.State;           // ICurrentUser
using NetSim.Core.Common;                 // EntityId

namespace NetSim.Application.Services;

// The rules of classes: who may create, join, leave, remove and delete
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
        _repository = repository;
        _currentUser = currentUser;
    }

    // Purpose: Returns the classes the signed-in user teaches.
    // Input:   nothing.
    // Output:  the classes, newest first.
    public IReadOnlyList<Classroom> GetTeachingClassrooms() =>
        _repository.GetAll()
            .Where(c => c.IsTeacher(_currentUser.Email))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();

    // Purpose: Returns the classes the signed-in user joined as a student.
    // Input:   nothing.
    // Output:  the classes, newest first.
    public IReadOnlyList<Classroom> GetJoinedClassrooms() =>
        _repository.GetAll()
            .Where(c => c.HasStudent(_currentUser.Email))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();

    // Purpose: Creates a class taught by the signed-in user.
    // Input:   name - the class name the teacher typed.
    // Output:  Success with the new class, or Failure with the reason.
    public OperationResult<Classroom> CreateClassroom(string name)
    {
        // An empty name, or only spaces
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, "Class name cannot be empty.");

        // Trim = remove the spaces at both ends
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, $"Class name cannot exceed {MaxNameLength} characters.");

        // A new class: a new id, a new code, and the signed-in user as its teacher
        var classroom = new Classroom(EntityId.New(), trimmed, NewUniqueJoinCode(), _currentUser.Email, DateTime.UtcNow);

        // Store it
        _repository.Save(classroom);

        return OperationResult<Classroom>.Success(classroom);
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
            return OperationResult<Classroom>.Failure(OperationErrorType.ValidationError, $"A join code is {JoinCodeLength} letters and digits.");

        // Find the class
        var classroom = _repository.GetByJoinCode(code);
        if (classroom is null)
            return OperationResult<Classroom>.Failure(OperationErrorType.NotFound, "No class has this code. Check it with your teacher.");

        // A teacher can't be a student in their own class
        if (classroom.IsTeacher(_currentUser.Email))
            return OperationResult<Classroom>.Failure(OperationErrorType.Conflict, "You are the teacher of this class.");

        // Already joined before
        if (classroom.HasStudent(_currentUser.Email))
            return OperationResult<Classroom>.Failure(OperationErrorType.Conflict, $"You already joined '{classroom.Name}'.");

        // Add the student and store the change
        classroom.AddStudent(_currentUser.Email, DateTime.UtcNow);
        _repository.Save(classroom);

        return OperationResult<Classroom>.Success(classroom);
    }

    // Purpose: Removes the signed-in user from a class they joined.
    // Input:   classroomId - the class to leave.
    // Output:  Success, or Failure with the reason.
    public OperationResult LeaveClassroom(EntityId classroomId)
    {
        var classroom = _repository.GetById(classroomId);
        if (classroom is null)
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");

        // RemoveStudent returns false if the user was not a student there
        if (!classroom.RemoveStudent(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "You are not a student in this class.");

        _repository.Save(classroom);
        return OperationResult.Success();
    }

    // Purpose: The teacher removes one student from their class.
    // Input:   classroomId - the class; studentEmail - the student to remove.
    // Output:  Success, or Failure with the reason.
    public OperationResult RemoveStudent(EntityId classroomId, string studentEmail)
    {
        var classroom = _repository.GetById(classroomId);
        if (classroom is null)
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");

        // Only the teacher of THIS class may remove its students
        if (!classroom.IsTeacher(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "Only the teacher of this class can remove students.");

        if (!classroom.RemoveStudent(studentEmail))
            return OperationResult.Failure(OperationErrorType.NotFound, "This student is not in the class.");

        _repository.Save(classroom);
        return OperationResult.Success();
    }

    // Purpose: The teacher deletes their class (its students simply stop being in it).
    // Input:   classroomId - the class to delete.
    // Output:  Success, or Failure with the reason.
    public OperationResult DeleteClassroom(EntityId classroomId)
    {
        var classroom = _repository.GetById(classroomId);
        if (classroom is null)
            return OperationResult.Failure(OperationErrorType.NotFound, "Class not found.");

        // Only the teacher of this class may delete it
        if (!classroom.IsTeacher(_currentUser.Email))
            return OperationResult.Failure(OperationErrorType.InvalidState, "Only the teacher of this class can delete it.");

        _repository.Delete(classroomId);
        return OperationResult.Success();
    }

    // Purpose: Makes a random join code that no other class uses yet.
    // Input:   nothing.
    // Output:  a 6-character code, e.g. "K7QMTX".
    // RandomNumberGenerator (and not new Random()) because the code is the only thing that lets
    // someone into a class - it must not be predictable. In stage 1 the client makes the code;
    // in the server stage the server will make it instead
    private string NewUniqueJoinCode()
    {
        while (true)
        {
            // Pick 6 random characters from the alphabet
            var chars = new char[JoinCodeLength];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)];

            var code = new string(chars);

            // Almost always free on the first try; if another class already has it, try again
            if (_repository.GetByJoinCode(code) is null)
                return code;
        }
    }
}

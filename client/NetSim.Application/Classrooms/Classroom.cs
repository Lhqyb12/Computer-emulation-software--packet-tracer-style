using System;                         // DateTime, StringComparison
using System.Collections.Generic;     // List, IReadOnlyList
using System.Linq;                    // Any, RemoveAll
using NetSim.Core.Common;             // EntityId

namespace NetSim.Application.Classrooms;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One class: a teacher, a join code, and the students who joined with that code.
// The role (teacher / student) belongs to the class, not to the user: the same user can teach one
// class and be a student in another
public sealed class Classroom
{
    // The students of this class. Private, so the only way to change it is through the methods below
    private readonly List<ClassroomMember> _students = new();

    // How many students the server says the class has, or null. A student gets only their own row in
    // Students (other students' emails stay private), so the real number comes separately
    private readonly int? _reportedStudentCount;

    // Purpose: Builds a class object.
    // Input:   id - its unique id; name - the class name; joinCode - the code students type to join;
    //          teacherEmail - the user who created it; createdAtUtc - when it was created;
    //          studentCount - optional: the number of students the server reported (leave it out = count the list).
    // Output:  none (a constructor) - a new Classroom with no students yet.
    // "int? studentCount = null" = an optional parameter: callers that don't pass it get null
    public Classroom(EntityId id, string name, string joinCode, string teacherEmail, DateTime createdAtUtc, int? studentCount = null)
    {
        Id = id;   // the unique id
        Name = name;   // the class name
        JoinCode = joinCode;   // the join code
        TeacherEmail = teacherEmail;   // who teaches it
        CreatedAtUtc = createdAtUtc;   // when it was created (UTC)
        _reportedStudentCount = studentCount;   // the server's number, or null
    }

    // How many students the class has: the server's number if it sent one, otherwise the length of the list.
    // "??" = if the left side is null, use the right side
    public int StudentCount => _reportedStudentCount ?? _students.Count;

    public EntityId Id { get; }               // the class's unique id
    public string Name { get; }               // e.g. "Networking 101"
    public string JoinCode { get; }           // e.g. "K7QMTX" - what the teacher shares with the students
    public string TeacherEmail { get; }       // the user who created the class and manages it
    public DateTime CreatedAtUtc { get; }     // when the class was created

    // The students, read-only from outside (IReadOnlyList has no Add/Remove)
    public IReadOnlyList<ClassroomMember> Students => _students;

    // Purpose: Checks whether a user is the teacher of this class.
    // Input:   email - the user's email.
    // Output:  true if this user created the class.
    // OrdinalIgnoreCase = "Dana@x.com" and "dana@x.com" are the same email
    public bool IsTeacher(string email) =>
        string.Equals(TeacherEmail, email, StringComparison.OrdinalIgnoreCase);   // is the teacher's email the same, ignoring upper/lower case?

    // Purpose: Checks whether a user is already a student in this class.
    // Input:   email - the user's email.
    // Output:  true if the user is in the students list.
    public bool HasStudent(string email) =>
        _students.Any(s => string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase));   // Any = is there at least one student with this email?

    // Purpose: Adds a student to the class.
    // Input:   email - the student's email; joinedAtUtc - when they joined.
    // Output:  nothing. The checks (already a member, the teacher himself) are done by ClassroomService.
    public void AddStudent(string email, DateTime joinedAtUtc) =>
        _students.Add(new ClassroomMember(email, joinedAtUtc));   // add a new student entry to the list

    // Purpose: Removes a student from the class.
    // Input:   email - the student's email.
    // Output:  true if the student was in the class and was removed.
    // RemoveAll returns how many items it removed; more than 0 = the student was there
    public bool RemoveStudent(string email) =>
        _students.RemoveAll(s => string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase)) > 0;   // remove every entry with this email; more than 0 removed = they were there
}

// One student in a class: who, and since when.
// A record = a small data-only class; the compiler writes the constructor and the properties
public sealed record ClassroomMember(string Email, DateTime JoinedAtUtc);

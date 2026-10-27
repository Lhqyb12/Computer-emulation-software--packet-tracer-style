using System;                           // DateTime
using System.Collections.Generic;       // IReadOnlyList
using System.Linq;                      // Select, ToList
using NetSim.Application.Classrooms;    // Classroom
using NetSim.Core.Common;               // EntityId

namespace NetSim.UI.ViewModels;

// The "row" classes of the Classes screen. Each one wraps a Classroom and offers it in a shape that
// is ready to show (a text instead of a number, local time instead of UTC), so the .axaml only prints

// One card in "Classes I teach"
public sealed class TeachingClassroomItem
{
    // Purpose: Builds the card for one class the user teaches.
    // Input:   classroom - the class.
    // Output:  none (a constructor).
    public TeachingClassroomItem(Classroom classroom)
    {
        Id = classroom.Id;
        Name = classroom.Name;
        JoinCode = classroom.JoinCode;
        CreatedAt = classroom.CreatedAtUtc.ToLocalTime();

        // One student row for every student, each one knowing which class it belongs to -
        // the "Remove" button needs both the class and the student
        Students = classroom.Students
            .Select(s => new ClassroomStudentItem(classroom.Id, s.Email, s.JoinedAtUtc.ToLocalTime()))
            .ToList();
    }

    public EntityId Id { get; }                              // the class id
    public string Name { get; }                              // the class name
    public string JoinCode { get; }                          // the code to share with the students
    public DateTime CreatedAt { get; }                       // when the class was created (local time)
    public IReadOnlyList<ClassroomStudentItem> Students { get; }

    // "1 student" / "3 students"
    public string StudentCountText => Students.Count == 1 ? "1 student" : $"{Students.Count} students";

    // Used by the .axaml to show the list, or the "no students yet" line instead
    public bool HasStudents => Students.Count > 0;
    public bool HasNoStudents => !HasStudents;
}

// One student line inside a teacher's class card
public sealed record ClassroomStudentItem(EntityId ClassroomId, string Email, DateTime JoinedAt);

// One card in "Classes I joined"
public sealed class JoinedClassroomItem
{
    // Purpose: Builds the card for one class the user joined.
    // Input:   classroom - the class; myEmail - the signed-in user, to find their own join date.
    // Output:  none (a constructor).
    public JoinedClassroomItem(Classroom classroom, string myEmail)
    {
        Id = classroom.Id;
        Name = classroom.Name;
        TeacherEmail = classroom.TeacherEmail;
        StudentCount = classroom.Students.Count;

        // Find my own row among the students to show when I joined.
        // "?." and "??" = if somehow not found, fall back to the class creation time
        var me = classroom.Students.FirstOrDefault(s => string.Equals(s.Email, myEmail, StringComparison.OrdinalIgnoreCase));
        JoinedAt = (me?.JoinedAtUtc ?? classroom.CreatedAtUtc).ToLocalTime();
    }

    public EntityId Id { get; }             // the class id
    public string Name { get; }             // the class name
    public string TeacherEmail { get; }     // who teaches it
    public int StudentCount { get; }        // how many students it has (including me)
    public DateTime JoinedAt { get; }       // when I joined (local time)
}

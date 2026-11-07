using System;                      // Guid, DateTime
using System.Collections.Generic;  // List<T>

namespace NetSim.App.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class messages as they travel over the socket - the mirror of the server's classroom DTOs.
// The field names must be the same as on the server: reading the JSON matches fields by name

// One class - matches ClassroomDto on the server
public class ClassroomDto
{
    public Guid Id { get; set; }                                     // the class's id
    public string Name { get; set; } = string.Empty;                 // the class name
    public string JoinCode { get; set; } = string.Empty;             // the join code (empty when I am a student)
    public string TeacherEmail { get; set; } = string.Empty;         // who teaches it
    public DateTime CreatedAtUtc { get; set; }                       // when it was created (UTC)
    public int StudentCount { get; set; }                            // how many students it has
    public List<ClassroomMemberDto> Students { get; set; } = new();  // the students I may see (only me, when I am a student)
}

// One student inside a ClassroomDto - matches ClassroomMemberDto on the server
public class ClassroomMemberDto
{
    public string Email { get; set; } = string.Empty;   // the student's email
    public DateTime JoinedAtUtc { get; set; }           // when they joined (UTC)
}

// The answer to "get-classrooms" - matches ClassroomListResponse on the server
public class ClassroomListResult
{
    public bool Success { get; set; }                            // true = here are the lists
    public string Message { get; set; } = string.Empty;          // the reason, when it failed
    public List<ClassroomDto> Teaching { get; set; } = new();    // the classes I teach; empty by default, never null
    public List<ClassroomDto> Joined { get; set; } = new();      // the classes I joined; empty by default, never null
}

// The answer to create / join / leave / remove-student / delete - matches ClassroomResponse on the server
public class ClassroomResult
{
    public bool Success { get; set; }                       // true = the action succeeded
    public string Message { get; set; } = string.Empty;     // the text to show when it failed
    public ClassroomDto? Classroom { get; set; }            // the class after create / join; null otherwise
}

namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One class as it travels over the socket to the client.
// What a user sees depends on their role in the class:
//   - the teacher gets everything: the join code and every student's email
//   - a student gets no join code, and in Students only their own row (with their join date) -
//     other students' emails are none of their business. StudentCount still tells how many there are
public class ClassroomDto
{
    public Guid Id { get; set; }                                // the class's id
    public string Name { get; set; } = string.Empty;            // the class name
    public string JoinCode { get; set; } = string.Empty;        // the join code (empty for a student)
    public string TeacherEmail { get; set; } = string.Empty;    // who teaches it
    public DateTime CreatedAtUtc { get; set; }                  // when it was created (UTC)
    public int StudentCount { get; set; }                       // how many students the class has
    public List<ClassroomMemberDto> Students { get; set; } = new();  // the students the caller may see
}

// One student inside a ClassroomDto
public class ClassroomMemberDto
{
    public string Email { get; set; } = string.Empty;   // the student's email
    public DateTime JoinedAtUtc { get; set; }           // when they joined (UTC)
}

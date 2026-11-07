namespace NetSim.Server.Models;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One student in one class, as a row in the ClassroomMembers table.
// A user who joined three classes has three rows here. The pair (ClassroomId, UserId) is the key of the
// table (see AppDbContext), so the same user can't be in the same class twice
public class ClassroomMemberRecord
{
    // Which class = the Id of a row in the Classrooms table (a foreign key)
    public Guid ClassroomId { get; set; }

    // The class itself (a navigation property - not a column)
    public ClassroomRecord? Classroom { get; set; }

    // Which student = the Id of a row in the Users table (a foreign key)
    public int UserId { get; set; }

    // The student itself (a navigation property - not a column). Used to read the student's email
    public User? User { get; set; }

    // When the student joined (UTC)
    public DateTime JoinedAtUtc { get; set; }
}

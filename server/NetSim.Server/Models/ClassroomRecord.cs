namespace NetSim.Server.Models;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One class, as a row in the Classrooms table. EF Core maps it by convention:
// class "ClassroomRecord" + DbSet "Classrooms" in AppDbContext -> table "Classrooms".
// The teacher is the user who created the class. The students are rows in ClassroomMembers
public class ClassroomRecord
{
    // The class's id. A Guid made by the server when the class is created
    public Guid Id { get; set; }

    // The class name, e.g. "Networking 101". Not unique - two classes may have the same name
    public string Name { get; set; } = string.Empty;

    // The 6-character code students type to join. Unique in the whole table (see AppDbContext)
    public string JoinCode { get; set; } = string.Empty;

    // Who teaches the class = the Id of a row in the Users table (a "foreign key").
    // The server fills it from the verified token, never from what the client sends
    public int TeacherUserId { get; set; }

    // The teacher itself. A "navigation property": not a column - EF Core uses it to understand that
    // TeacherUserId points to a User, and to read the teacher's email in queries
    public User? Teacher { get; set; }

    // When the class was created (UTC)
    public DateTime CreatedAtUtc { get; set; }

    // The students of this class: every ClassroomMembers row whose ClassroomId is this class's Id.
    // Also a navigation property - no column. "= new()" = starts as an empty list, never null
    public List<ClassroomMemberRecord> Members { get; set; } = new();
}

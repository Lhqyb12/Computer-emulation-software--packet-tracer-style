using Microsoft.EntityFrameworkCore;  // DbContext, DbSet<T>, DbContextOptions - the whole ORM (Entity Framework Core) infrastructure
using NetSim.Server.Models;           // The User model this context manages

namespace NetSim.Server.Data;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// DbContext is the single "gateway" to the database: it implements both Unit of Work (tracks changes and writes
// them together via SaveChanges in one transaction) and acts as a Repository (access to Users as if it were an
// in-memory collection)
public class AppDbContext : DbContext
{
    // Purpose: Builds the database context with the connection settings from Program.cs.
    // Input:   options - the EF Core options (the PostgreSQL connection string).
    // Output:  none (a constructor) - a new AppDbContext.
    // The constructor does no logic itself - it just forwards (base(options)) the DbContextOptions
    // (which holds the Postgres connection string, coming from builder.Services.AddDbContext in Program.cs)
    // to the base DbContext constructor. This way AppDbContext itself doesn't "know" the connection details -
    // they are injected from outside (Dependency Injection), which for example lets tests inject a different
    // connection (like an in-memory database) without changing this class at all
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSet<User> represents the Users table in the database.
    // Writing "=> Set<User>()" (expression-bodied property) is equivalent to { get { return Set<User>(); } }
    // but more concise - Set<User>() is the generic method that returns/creates the matching DbSet according to
    // EF Core's conventions (the table name is derived from the class name User -> "Users", without me having
    // to define that explicitly)
    public DbSet<User> Users => Set<User>();
    
    // The SecurityEvents table - the audit log shown in the dashboard's Security tab (see Models/SecurityEvent.cs)
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    
    // The Projects table - the simulator projects every user saves (see Models/ProjectRecord.cs)
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    // The Classrooms table - one row per class (see Models/ClassroomRecord.cs)
    public DbSet<ClassroomRecord> Classrooms => Set<ClassroomRecord>();

    // The ClassroomMembers table - one row per student in a class (see Models/ClassroomMemberRecord.cs)
    public DbSet<ClassroomMemberRecord> ClassroomMembers => Set<ClassroomMemberRecord>();



    // Purpose: Adds database rules EF Core cannot guess by itself (unique indexes, keys, and the links between tables).
    // Input:   modelBuilder - the object EF Core uses to describe the tables.
    // Output:  nothing.
    // Called by EF Core once, when it builds its picture of the database. This is where rules are added
    // that the conventions cannot guess. "protected override" = we replace the empty version DbContext has.
    //
    // A real uniqueness constraint at the level of the database itself - Postgres will refuse any attempt
    // to insert a second row with the same Email, even if (for whatever reason) the check in the code in
    // AuthService was missed
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // On the Users table: an index on the Email column, and no two rows may have the same value in it
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // The same for GoogleId: one Google account can belong to one user only.
        // (Rows where GoogleId is null do not clash with each other)
        modelBuilder.Entity<User>()
            .HasIndex(u => u.GoogleId)
            .IsUnique();

        
        // The Projects table. Based on ConfigureProjects in the NetworkSimulator project, plus the owner
        modelBuilder.Entity<ProjectRecord>(entity =>
        {
            // The client creates the Guid (EntityId.New()) - the database must store it as is, not make its own
            entity.Property(p => p.Id).ValueGeneratedNever();

            // A project must have a name ("NOT NULL" in the database)
            entity.Property(p => p.Name).IsRequired();

            // Every project belongs to one user, and a user can have many projects ("one-to-many").
            // Cascade = when a user is deleted (the admin's "Delete user"), their projects are deleted with
            // them, instead of being left behind pointing at a user that no longer exists
            entity.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // The same name may exist once per owner: two users may both have "Lab 1", one user may not have
            // it twice. The index is on the pair (OwnerUserId, Name), not on Name alone
            entity.HasIndex(p => new { p.OwnerUserId, p.Name }).IsUnique();

            // Speeds up the project list, which is always sorted by "last modified" first
            entity.HasIndex(p => p.LastModifiedAtUtc);
        });

        // The Classrooms table
        modelBuilder.Entity<ClassroomRecord>(entity =>
        {
            // The server creates the Guid itself (Guid.NewGuid()) - the database stores it as is
            entity.Property(c => c.Id).ValueGeneratedNever();

            // A class must have a name and a join code ("NOT NULL" in the database)
            entity.Property(c => c.Name).IsRequired();
            entity.Property(c => c.JoinCode).IsRequired();

            // No two classes may have the same join code - otherwise a code could lead to two classes.
            // The service checks this before saving; this index is the safety net in the database itself
            entity.HasIndex(c => c.JoinCode).IsUnique();

            // Every class has one teacher, and a user can teach many classes ("one-to-many").
            // Cascade = when the teacher's account is deleted, their classes are deleted with it
            entity.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // The ClassroomMembers table
        modelBuilder.Entity<ClassroomMemberRecord>(entity =>
        {
            // The key is the pair (class, student): the same student can be in a class only once.
            // "new { ... }" = a small nameless object that only lists the two columns
            entity.HasKey(m => new { m.ClassroomId, m.UserId });

            // Every row belongs to one class, and a class has many rows (its Members list).
            // Cascade = deleting a class deletes its member rows - the students simply stop being in it
            entity.HasOne(m => m.Classroom)
                .WithMany(c => c.Members)
                .HasForeignKey(m => m.ClassroomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Every row belongs to one user. Cascade = deleting a user removes them from all their classes
            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


    }

   

}

using Microsoft.EntityFrameworkCore;  // DbContext, DbSet<T>, DbContextOptions - the whole ORM (Entity Framework Core) infrastructure
using NetSim.Server.Models;           // The User model this context manages

namespace NetSim.Server.Data;

// DbContext is the single "gateway" to the database: it implements both Unit of Work (tracks changes and writes
// them together via SaveChanges in one transaction) and acts as a Repository (access to Users as if it were an
// in-memory collection)
public class AppDbContext : DbContext
{
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

    }

   

}

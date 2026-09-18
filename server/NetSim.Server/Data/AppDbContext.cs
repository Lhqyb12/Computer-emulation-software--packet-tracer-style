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

    // אילוץ ייחודיות אמיתי ברמת המסד עצמו - Postgres ידחה כל ניסיון להכניס שורה שנייה עם אותו Email,
    // גם אם (מסיבה כלשהי) הבדיקה בקוד ב-AuthService פוספסה מכל סיבה שהיא
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }

   

}

using System;                           // Guid, DateTime, StringComparison
using System.Collections.Generic;       // Dictionary, IReadOnlyList
using System.Linq;                      // FirstOrDefault, ToList
using NetSim.Application.Classrooms;    // Classroom
using NetSim.Application.Persistence;   // IClassroomRepository
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;

// Stage-1 stand-in for IClassroomRepository: keeps the classes in memory while the app runs.
// Registered as a Singleton (one for the whole app, not one per sign-in), so a class created by one
// user is still there after sign-out - another user can sign in on the same app and join it.
// The server stage will replace this class; nothing else has to change
public sealed class InMemoryClassroomRepository : IClassroomRepository
{
    // The classes. Key = the class id (a Guid), value = the class
    private readonly Dictionary<Guid, Classroom> _classrooms = new();

    // Purpose: Builds the repository with one demo class already in it.
    // Input:   nothing.
    // Output:  none (a constructor).
    // The demo class lets a single user try "Join a class" right away, with the code NETWRK
    public InMemoryClassroomRepository()
    {
        var demo = new Classroom(
            EntityId.New(),
            "Networking 101 (demo)",
            "NETWRK",
            "teacher@netsim.demo",
            DateTime.UtcNow.AddDays(-7));

        // Two made-up students, so the class doesn't look empty
        demo.AddStudent("noa@netsim.demo", DateTime.UtcNow.AddDays(-6));
        demo.AddStudent("yossi@netsim.demo", DateTime.UtcNow.AddDays(-5));

        Save(demo);
    }

    // Purpose: Returns every stored class.
    // Input:   nothing.
    // Output:  a read-only list of classes.
    public IReadOnlyList<Classroom> GetAll() => _classrooms.Values.ToList();

    // Purpose: Finds one class by its id.
    // Input:   id - the class's EntityId.
    // Output:  the class, or null.
    public Classroom? GetById(EntityId id) => _classrooms.GetValueOrDefault(id.Value);

    // Purpose: Finds one class by its join code, ignoring upper/lower case.
    // Input:   joinCode - the code.
    // Output:  the class, or null.
    public Classroom? GetByJoinCode(string joinCode) =>
        _classrooms.Values.FirstOrDefault(c => string.Equals(c.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase));

    // Purpose: Adds a class, or replaces the stored one with the same id.
    // Input:   classroom - the class to store.
    // Output:  nothing.
    public void Save(Classroom classroom) => _classrooms[classroom.Id.Value] = classroom;

    // Purpose: Removes the class with this id.
    // Input:   id - the class's EntityId.
    // Output:  true if it existed and was removed.
    public bool Delete(EntityId id) => _classrooms.Remove(id.Value);
}

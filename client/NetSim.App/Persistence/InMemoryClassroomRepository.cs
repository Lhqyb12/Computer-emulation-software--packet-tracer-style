using System;                           // Guid, DateTime, StringComparison
using System.Collections.Generic;       // Dictionary, IReadOnlyList
using System.Linq;                      // FirstOrDefault, ToList
using NetSim.Application.Classrooms;    // Classroom
using NetSim.Application.Persistence;   // IClassroomRepository
using NetSim.Core.Common;               // EntityId

namespace NetSim.App.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

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
        var demo = new Classroom(   // the demo class:
            EntityId.New(),   // a new id
            "Networking 101 (demo)",   // its name
            "NETWRK",   // its fixed join code
            "teacher@netsim.demo",   // a made-up teacher (nobody can sign in as this)
            DateTime.UtcNow.AddDays(-7));   // created a week ago

        // Two made-up students, so the class doesn't look empty
        demo.AddStudent("noa@netsim.demo", DateTime.UtcNow.AddDays(-6));   // made-up student, joined 6 days ago
        demo.AddStudent("yossi@netsim.demo", DateTime.UtcNow.AddDays(-5));   // made-up student, joined 5 days ago

        Save(demo);   // store it in the dictionary
    }

    // Purpose: Returns every stored class.
    // Input:   nothing.
    // Output:  a read-only list of classes.
    public IReadOnlyList<Classroom> GetAll() => _classrooms.Values.ToList();   // every value of the dictionary, as a new list

    // Purpose: Finds one class by its id.
    // Input:   id - the class's EntityId.
    // Output:  the class, or null.
    public Classroom? GetById(EntityId id) => _classrooms.GetValueOrDefault(id.Value);   // the value for this key, or null

    // Purpose: Finds one class by its join code, ignoring upper/lower case.
    // Input:   joinCode - the code.
    // Output:  the class, or null.
    public Classroom? GetByJoinCode(string joinCode) =>
        _classrooms.Values.FirstOrDefault(c => string.Equals(c.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase));   // the first class with this code, or null

    // Purpose: Adds a class, or replaces the stored one with the same id.
    // Input:   classroom - the class to store.
    // Output:  nothing.
    public void Save(Classroom classroom) => _classrooms[classroom.Id.Value] = classroom;   // add, or replace the one with this id

    // Purpose: Removes the class with this id.
    // Input:   id - the class's EntityId.
    // Output:  true if it existed and was removed.
    public bool Delete(EntityId id) => _classrooms.Remove(id.Value);   // remove it; true if it was there
}

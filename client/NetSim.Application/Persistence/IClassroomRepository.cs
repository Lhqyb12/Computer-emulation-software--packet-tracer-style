using System.Collections.Generic;     // IReadOnlyList
using NetSim.Application.Classrooms;  // Classroom
using NetSim.Core.Common;             // EntityId

namespace NetSim.Application.Persistence;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Where the classes are stored. Only an interface here: stage 1 uses an in-memory list
// (NetSim.App/Persistence/InMemoryClassroomRepository.cs); a later stage will talk to the server
public interface IClassroomRepository
{
    // Purpose: Reads all the classes.
    // Input:   nothing.
    // Output:  a read-only list of every class.
    IReadOnlyList<Classroom> GetAll();

    // Purpose: Finds one class by its id.
    // Input:   id - the class's id.
    // Output:  the class, or null if there is none.
    Classroom? GetById(EntityId id);

    // Purpose: Finds one class by its join code (upper/lower case ignored).
    // Input:   joinCode - the code.
    // Output:  the class, or null if no class has this code.
    Classroom? GetByJoinCode(string joinCode);

    // Purpose: Adds a new class, or replaces the stored one with the same id.
    // Input:   classroom - the class to store.
    // Output:  nothing.
    void Save(Classroom classroom);

    // Purpose: Removes one class.
    // Input:   id - the class's id.
    // Output:  true if it existed and was removed.
    bool Delete(EntityId id);
}

using System.Collections.Generic;     // IReadOnlyList
using NetSim.Application.Classrooms;  // Classroom
using NetSim.Core.Common;             // EntityId

namespace NetSim.Application.Persistence;

// Where the classes are stored. Only an interface here: stage 1 uses an in-memory list
// (NetSim.App/Persistence/InMemoryClassroomRepository.cs); a later stage will talk to the server
public interface IClassroomRepository
{
    // All the classes
    IReadOnlyList<Classroom> GetAll();

    // The class with this id, or null
    Classroom? GetById(EntityId id);

    // The class with this join code (upper/lower case ignored), or null
    Classroom? GetByJoinCode(string joinCode);

    // Adds a new class, or replaces the stored one with the same id
    void Save(Classroom classroom);

    // Removes the class with this id. true = it existed
    bool Delete(EntityId id);
}

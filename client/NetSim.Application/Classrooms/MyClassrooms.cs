using System.Collections.Generic;   // IReadOnlyList

namespace NetSim.Application.Classrooms;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Both halves of the Classes screen, read together in one request to the server:
// Teaching = the classes I teach; Joined = the classes I joined as a student.
// A record = a small data-only class; the compiler writes the constructor and the properties
public sealed record MyClassrooms(IReadOnlyList<Classroom> Teaching, IReadOnlyList<Classroom> Joined);

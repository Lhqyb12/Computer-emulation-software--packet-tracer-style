namespace NetSim.Core.Common;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The unique id of an entity (a project, a class, later a device). Kept separate from any name,
// because names can change and are not always unique.
// "readonly" = can't change after it is made; "record" = two ids are equal when their values are
// equal; "struct" = copied by value, like a number
public readonly record struct EntityId
{
    // The real unique value: a GUID, e.g. "8c2a1f3e-45b0-4a12-9c3d-...".
    public Guid Value { get; }

    // Purpose: Builds an id around a Guid. Private: no outside code can build an EntityId directly -
    //          the only doors in are New() and FromValue() below.
    // Input:   value - the Guid.
    // Output:  none (a constructor).
    private EntityId(Guid value)
    {
        Value = value;   // keep the Guid
    }

    // Purpose: Makes a brand-new, random, unique id. This is how every new entity gets its Id.
    // Input:   nothing.
    // Output:  a new EntityId. Guid.NewGuid() = ask the operating system for a random Guid
    public static EntityId New() => new(Guid.NewGuid());

    // Purpose: Rebuilds an id that already exists - only for loading something that was saved before
    //          (e.g. a project that came back from the server). New things always use New().
    // Input:   value - the saved Guid.
    // Output:  an EntityId with that value.
    public static EntityId FromValue(Guid value) => new(value);

    // Purpose: Turns the id into text (e.g. for a log line).
    // Input:   nothing.
    // Output:  the Guid as text. "override" = replaces the default text a record would make
    public override string ToString() => Value.ToString();
}

namespace NetSim.Core.Common;

/// <summary>
/// Strongly-typed unique identifier for core domain entities. Kept distinct from any
/// human-readable name, since names are not guaranteed to be unique across contexts.
/// </summary>
public readonly record struct EntityId
{
    // The real unique value: a GUID, e.g. "8c2a1f3e-45b0-4a12-9c3d-...".
    public Guid Value { get; }

    // Constructor is private: no outside code can build an EntityId directly.
    // The only door in is the New() factory method below.
    private EntityId(Guid value)
    {
        Value = value;
    }

    // Generates a fresh, random, guaranteed-unique id. This is how every
    // entity (device, interface, connection, ...) gets its Id.
    public static EntityId New() => new(Guid.NewGuid());

    // Reconstructs an id previously produced by New() - the only legitimate reason to
    // need this is persistence/mapping code materializing an entity that already has an
    // id (e.g. loading it back from LiteDB). Application/domain code should still only
    // ever mint identities via New(); this is not a way around that.
    public static EntityId FromValue(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

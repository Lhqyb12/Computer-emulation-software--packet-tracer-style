namespace NetSim.Application.Persistence;

/// <summary>
/// Persists the single <see cref="UserPreferences"/> document local to this machine. This is
/// the first concrete repository built on top of the Phase 6 persistence infrastructure -
/// deliberately small in scope (Phase 7 introduces project/network persistence on the same
/// pattern: an Application-layer interface here, a LiteDB-backed implementation in
/// NetworkSimulator.Infrastructure, and nothing above this layer ever referencing LiteDB
/// directly).
/// </summary>
public interface IUserPreferencesRepository
{
    /// <summary>Returns the stored preferences, or null if none have been saved yet.</summary>
    UserPreferences? Get();

    /// <summary>Creates or overwrites the stored preferences.</summary>
    void Save(UserPreferences preferences);

    /// <summary>Removes the stored preferences, if any. Returns true if a document was deleted.</summary>
    bool Delete();
}

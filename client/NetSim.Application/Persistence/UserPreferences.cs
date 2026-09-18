using NetSim.Core.Common;

namespace NetSim.Application.Persistence;

/// <summary>
/// Application-level user preferences (as opposed to network-domain state, which lives in
/// <see cref="Core.Topology.Network"/>). There is exactly one of these per local database -
/// <see cref="IUserPreferencesRepository"/> treats it as a singleton document, not a collection
/// of many records. Kept intentionally small; grow it as real preferences (beyond the
/// theme selection deferred in Phase 5) actually need to be remembered across sessions.
/// </summary>
public sealed class UserPreferences
{
    public UserPreferences(EntityId id, string theme)
    {
        Id = id;
        Theme = theme;
    }

    public EntityId Id { get; }

    public string Theme { get; set; }
}

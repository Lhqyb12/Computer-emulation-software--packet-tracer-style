namespace NetSim.Application.Routing;

/// <summary>
/// Whether a configured static route (Phase 28) can currently be used for forwarding, and if not,
/// why - so the UI can show a meaningful status and keep an unusable route visible rather than
/// deleting it (brief sections 15, 24, 30).
/// </summary>
public enum StaticRouteStatus
{
    /// <summary>The route resolves to an operational outgoing interface and a reachable next hop - it is in use.</summary>
    Active,

    /// <summary>The route's explicit outgoing interface is administratively or operationally down.</summary>
    InterfaceDown,

    /// <summary>The route's next hop cannot currently be reached through any other route (or a next-hop cycle was detected).</summary>
    UnreachableNextHop,

    /// <summary>The route is configured but not usable for another reason.</summary>
    Inactive,
}

namespace NetSim.Core.Networking;

/// <summary>
/// Decides whether two <see cref="NetworkInterface"/> endpoints may be joined by a
/// <see cref="Connection"/>, and which <see cref="ConnectionType"/> medium best fits the pair.
/// Compatibility is driven by <see cref="InterfaceCapability"/> - two interfaces are cable-
/// compatible when their capability (media-family) sets overlap - so a new interface type only
/// needs its capabilities declared in <see cref="InterfaceTypeInfo"/>, not a new row in a matrix
/// here. Rejections come back with a human-readable reason so the UI can explain them. This is a
/// physical-media check only; speed is deliberately not matched (see the Phase 14 brief - real
/// Ethernet auto-negotiates), and administrative state is checked by the
/// <see cref="NetworkInterface"/> overloads.
/// </summary>
public static class ConnectionCompatibility
{
    // ----- Interface-type overloads (media family only) -----

    public static bool AreCompatible(InterfaceType a, InterfaceType b) => AreCompatible(a, b, out _);

    public static bool AreCompatible(InterfaceType a, InterfaceType b, out string? incompatibilityReason)
    {
        incompatibilityReason = null;

        if ((InterfaceTypeInfo.Capabilities(a) & InterfaceTypeInfo.Capabilities(b)) != InterfaceCapability.None)
        {
            return true;
        }

        incompatibilityReason =
            $"A {InterfaceTypeInfo.DisplayName(a)} interface cannot be connected to a {InterfaceTypeInfo.DisplayName(b)} interface.";
        return false;
    }

    // ----- Interface overloads (media family + administrative state) -----

    public static bool AreCompatible(NetworkInterface a, NetworkInterface b) => AreCompatible(a, b, out _);

    public static bool AreCompatible(NetworkInterface a, NetworkInterface b, out string? incompatibilityReason)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        incompatibilityReason = null;

        if (!a.IsEnabled)
        {
            incompatibilityReason = $"Interface '{a.Name}' is administratively disabled.";
            return false;
        }

        if (!b.IsEnabled)
        {
            incompatibilityReason = $"Interface '{b.Name}' is administratively disabled.";
            return false;
        }

        return AreCompatible(a.InterfaceType, b.InterfaceType, out incompatibilityReason);
    }

    /// <summary>The connection medium that best fits a link between the two interface types.</summary>
    public static ConnectionType InferConnectionType(InterfaceType a, InterfaceType b)
    {
        if (a == InterfaceType.Serial && b == InterfaceType.Serial)
        {
            return ConnectionType.Serial;
        }

        return ConnectionType.Copper;
    }
}

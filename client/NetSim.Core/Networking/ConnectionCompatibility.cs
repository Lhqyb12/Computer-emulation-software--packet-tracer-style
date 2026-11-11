namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Decides whether two ports may be joined by a cable, and which cable material fits them.
// The rule: two port kinds fit when they share at least one cable option (InterfaceCapability).
// When they don't fit, it also gives a reason the screen can show the user.
// "static class" = no objects, just call ConnectionCompatibility.Something(...)
public static class ConnectionCompatibility
{
    // ----- By port KIND (only the cable options) -----

    // Purpose: Do two port kinds fit, without needing the reason.
    // Input:   a, b - the two port kinds.
    // Output:  true when they fit. "out _" = we don't need the reason, throw it away
    public static bool AreCompatible(InterfaceType a, InterfaceType b) => AreCompatible(a, b, out _);

    // Purpose: Do two port kinds fit, and why not when they don't.
    // Input:   a, b - the two port kinds; incompatibilityReason - filled by the method ("out").
    // Output:  true when they fit (reason = null); false + a reason when they don't.
    public static bool AreCompatible(InterfaceType a, InterfaceType b, out string? incompatibilityReason)
    {
        // No reason yet
        incompatibilityReason = null;

        // "&" = the options BOTH kinds have. Not None = they share at least one - they fit
        if ((InterfaceTypeInfo.Capabilities(a) & InterfaceTypeInfo.Capabilities(b)) != InterfaceCapability.None)
        {
            return true;
        }

        // Nothing shared - write the reason, e.g. "A Serial interface cannot be connected to a Fast Ethernet interface."
        incompatibilityReason =
            $"A {InterfaceTypeInfo.DisplayName(a)} interface cannot be connected to a {InterfaceTypeInfo.DisplayName(b)} interface.";
        return false;
    }

    // ----- By real PORTS (the cable options + turned on) -----

    // Purpose: Do two real ports fit, without needing the reason.
    // Input:   a, b - the two ports.
    // Output:  true when they fit.
    public static bool AreCompatible(NetworkInterface a, NetworkInterface b) => AreCompatible(a, b, out _);

    // Purpose: Do two real ports fit: both must be turned on, and their kinds must fit.
    // Input:   a, b - the two ports; incompatibilityReason - filled by the method ("out").
    // Output:  true when they fit; false + a reason when they don't.
    public static bool AreCompatible(NetworkInterface a, NetworkInterface b, out string? incompatibilityReason)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // No reason yet
        incompatibilityReason = null;

        // The first port is turned off
        if (!a.IsEnabled)
        {
            incompatibilityReason = $"Interface '{a.Name}' is administratively disabled.";
            return false;
        }

        // The second port is turned off
        if (!b.IsEnabled)
        {
            incompatibilityReason = $"Interface '{b.Name}' is administratively disabled.";
            return false;
        }

        // Both on - check their kinds with the method above
        return AreCompatible(a.InterfaceType, b.InterfaceType, out incompatibilityReason);
    }

    // Purpose: Picks the cable material that fits two port kinds.
    // Input:   a, b - the two port kinds.
    // Output:  Serial when both are Serial ports, otherwise Copper.
    public static ConnectionType InferConnectionType(InterfaceType a, InterfaceType b)
    {
        // Two serial ports - a serial cable
        if (a == InterfaceType.Serial && b == InterfaceType.Serial)
        {
            return ConnectionType.Serial;
        }

        // Anything else - a normal copper cable
        return ConnectionType.Copper;
    }
}

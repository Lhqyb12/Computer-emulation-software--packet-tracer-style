namespace NetSim.Core.Networking;

/// <summary>
/// The single place that maps an <see cref="InterfaceType"/> to its default characteristics -
/// human-readable name, nominal <see cref="InterfaceSpeed"/>, media <see cref="InterfaceCapability"/>
/// set, and the short name prefix used for compact display ("G", "Fa", "Se", ...). Keeping this
/// out of <see cref="NetworkInterface"/> means a new interface type is one entry here, not edits
/// scattered across the model, the UI and the persistence mapper.
/// </summary>
public static class InterfaceTypeInfo
{
    public static string DisplayName(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => "Ethernet",
        InterfaceType.FastEthernet => "Fast Ethernet",
        InterfaceType.GigabitEthernet => "Gigabit Ethernet",
        InterfaceType.Serial => "Serial",
        InterfaceType.Console => "Console",
        _ => type.ToString(),
    };

    public static InterfaceSpeed DefaultSpeed(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => InterfaceSpeed.Mbps10,
        InterfaceType.FastEthernet => InterfaceSpeed.Mbps100,
        InterfaceType.GigabitEthernet => InterfaceSpeed.Gbps1,
        InterfaceType.Serial => InterfaceSpeed.SerialT1,
        InterfaceType.Console => InterfaceSpeed.Console9600,
        _ => InterfaceSpeed.Mbps100,
    };

    public static InterfaceCapability Capabilities(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => InterfaceCapability.Ethernet,
        InterfaceType.FastEthernet => InterfaceCapability.Ethernet,
        InterfaceType.GigabitEthernet => InterfaceCapability.Ethernet | InterfaceCapability.Fiber,
        InterfaceType.Serial => InterfaceCapability.Serial,
        InterfaceType.Console => InterfaceCapability.Console,
        _ => InterfaceCapability.None,
    };

    /// <summary>Short-name prefix for compact labels, e.g. "GigabitEthernet0/0" -> "G0/0".</summary>
    public static string ShortPrefix(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => "Eth",
        InterfaceType.FastEthernet => "Fa",
        InterfaceType.GigabitEthernet => "G",
        InterfaceType.Serial => "Se",
        InterfaceType.Console => "Con",
        _ => type.ToString(),
    };

    /// <summary>
    /// Derives a compact display name from a full interface name by swapping the long type prefix
    /// for the short one ("GigabitEthernet0/1" -> "G0/1"). Falls back to the full name when it does
    /// not start with the expected long prefix, so no assumption is made about the numeric portion.
    /// </summary>
    public static string ShortName(InterfaceType type, string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        var longPrefix = type.ToString();
        if (fullName.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase) && fullName.Length > longPrefix.Length)
        {
            return ShortPrefix(type) + fullName[longPrefix.Length..];
        }

        return fullName;
    }
}

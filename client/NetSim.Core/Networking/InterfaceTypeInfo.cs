namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that says, for every InterfaceType: its display name, its default speed,
// which cables it takes, and its short name ("G", "Fa", "Se"...).
// Adding a new port type later = one new line in each method here, not changes all over the project.
// "static class" = no objects, just call InterfaceTypeInfo.Something(...)
public static class InterfaceTypeInfo
{
    // Purpose: The name of a port type, as shown on the screen.
    // Input:   type - the port type.
    // Output:  e.g. "Fast Ethernet". "switch" here = pick the line that matches the type
    public static string DisplayName(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => "Ethernet",                    // old Ethernet
        InterfaceType.FastEthernet => "Fast Ethernet",           // 100 Mbps
        InterfaceType.GigabitEthernet => "Gigabit Ethernet",     // 1 Gbps
        InterfaceType.Serial => "Serial",                        // serial line
        InterfaceType.Console => "Console",                      // management port
        _ => type.ToString(),                                    // "_" = any other value - use its own name
    };

    // Purpose: The speed a new port of this type starts with.
    // Input:   type - the port type.
    // Output:  the default speed (InterfaceSpeed).
    public static InterfaceSpeed DefaultSpeed(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => InterfaceSpeed.Mbps10,           // 10 Mbps
        InterfaceType.FastEthernet => InterfaceSpeed.Mbps100,      // 100 Mbps
        InterfaceType.GigabitEthernet => InterfaceSpeed.Gbps1,     // 1 Gbps
        InterfaceType.Serial => InterfaceSpeed.SerialT1,           // 1.544 Mbps
        InterfaceType.Console => InterfaceSpeed.Console9600,       // 9.6 Kbps
        _ => InterfaceSpeed.Mbps100,                               // anything else - 100 Mbps
    };

    // Purpose: Which kinds of cable a port of this type can take.
    // Input:   type - the port type.
    // Output:  the options; "|" = put two options together in one value.
    public static InterfaceCapability Capabilities(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => InterfaceCapability.Ethernet,                                    // copper only
        InterfaceType.FastEthernet => InterfaceCapability.Ethernet,                                // copper only
        InterfaceType.GigabitEthernet => InterfaceCapability.Ethernet | InterfaceCapability.Fiber, // copper OR fiber
        InterfaceType.Serial => InterfaceCapability.Serial,                                        // serial only
        InterfaceType.Console => InterfaceCapability.Console,                                      // console only
        _ => InterfaceCapability.None,                                                             // anything else - nothing
    };

    // Purpose: The short prefix used in compact labels on the canvas.
    // Input:   type - the port type.
    // Output:  e.g. "G" for GigabitEthernet.
    public static string ShortPrefix(InterfaceType type) => type switch
    {
        InterfaceType.Ethernet => "Eth",          // Ethernet0/0 -> Eth0/0
        InterfaceType.FastEthernet => "Fa",       // FastEthernet0/1 -> Fa0/1
        InterfaceType.GigabitEthernet => "G",     // GigabitEthernet0/0 -> G0/0
        InterfaceType.Serial => "Se",             // Serial0/0/0 -> Se0/0/0
        InterfaceType.Console => "Con",           // Console -> Con
        _ => type.ToString(),                     // anything else - its own name
    };

    // Purpose: Makes the short name of a port from its full name.
    // Input:   type - the port type; fullName - e.g. "GigabitEthernet0/1".
    // Output:  e.g. "G0/1". When the full name does not start with the type's name, it is returned as is.
    public static string ShortName(InterfaceType type, string fullName)
    {
        // No name - nothing to shorten
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        // The long prefix = the type's own name, e.g. "GigabitEthernet"
        var longPrefix = type.ToString();

        // The name starts with the long prefix (ignoring upper/lower case) and has something after it
        if (fullName.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase) && fullName.Length > longPrefix.Length)
        {
            // Short prefix + the rest. "fullName[n..]" = the text from position n to the end, e.g. "0/1"
            return ShortPrefix(type) + fullName[longPrefix.Length..];
        }

        // Does not match - keep the full name
        return fullName;
    }
}

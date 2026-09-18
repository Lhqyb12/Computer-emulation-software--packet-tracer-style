namespace NetSim.Core.Devices;

/// <summary>
/// Identifies the concrete kind of a <see cref="NetworkDevice"/> without requiring
/// callers to perform type checks against the class hierarchy.
/// </summary>
// A fixed list of the possible device kinds. Every NetworkDevice must be exactly
// one of these values - nothing else is allowed.
public enum DeviceType
{
    Router,
    Switch,
    Pc,
    Server,
    Laptop,
}

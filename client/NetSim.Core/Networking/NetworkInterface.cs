using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;

namespace NetSim.Core.Networking;

/// <summary>
/// A network interface belonging to exactly one <see cref="NetworkDevice"/>. Instances
/// are only ever created through <see cref="NetworkDevice.AddInterface"/>, which keeps
/// the device-to-interface relationship single-owner and consistent.
///
/// An interface carries meaningful physical-layer state (see the Phase 14 brief): a nominal
/// <see cref="Speed"/>, a media <see cref="Capabilities"/> set that governs which other interfaces
/// it may be cabled to, an <see cref="AdministrativeState"/> (operator enabled/disabled) that is
/// kept distinct from the <see cref="OperationalState"/> (link up/down), and derived
/// <see cref="IsAvailable"/> / <see cref="IsOperational"/> views used by the connection system and
/// the UI. Addressing (IP/MAC), framing and forwarding are still intentionally out of scope.
/// </summary>
// "sealed" = no other class is allowed to inherit from this one. There is no
// planned specialization for a network interface, so we lock the door on purpose.
public sealed class NetworkInterface
{
    // The interface's IPv4 address assignments (Phase 18). The list is private so the "exactly
    // one primary" rule and duplicate-address rejection stay enforced here; callers get a
    // read-only view via IPv4Configurations. Empty until AddIPv4Configuration is called, and
    // never populated automatically on device creation - an interface has no IP until one is
    // configured, exactly like a real device.
    private readonly List<Ipv4InterfaceConfiguration> _ipv4Configurations = [];

    // The interface's IPv6 address assignments (Phase 19). Same design as the IPv4 list above:
    // private so the "exactly one primary" rule and duplicate-address rejection stay enforced
    // here, empty until AddIPv6Configuration is called, never populated automatically (no SLAAC,
    // no EUI-64). IPv6 interfaces routinely carry more than one address (a link-local plus one or
    // more global / unique-local addresses), so the list carries its weight here more than IPv4's.
    private readonly List<Ipv6InterfaceConfiguration> _ipv6Configurations = [];

    // The interface's ARP cache (Phase 20): the IPv4 -> MAC mappings it has learned on its own
    // Layer 2 segment. Per-interface on purpose - a device with several interfaces must never mix
    // mappings across unrelated L2 domains (Phase 20 brief, "Multiple Interfaces"). Always present
    // (empty until ARP traffic populates it). NOT persisted: dynamic ARP state is transient and is
    // rebuilt from live traffic after a restart - see docs/architecture/arp-engine.md.
    private readonly ArpCache _arpCache = new();

    // "internal" constructor = only code inside the Core project can call it.
    // In practice, only NetworkDevice.AddInterface() ever does - this is what
    // makes "every interface has exactly one owning device" unbreakable from
    // outside code.
    internal NetworkInterface(string name, InterfaceType interfaceType, NetworkDevice device)
    {
        Id = EntityId.New();
        Name = name;
        InterfaceType = interfaceType;
        Device = device;
        Speed = InterfaceTypeInfo.DefaultSpeed(interfaceType);
        Capabilities = InterfaceTypeInfo.Capabilities(interfaceType);
        AdministrativeState = InterfaceAdministrativeState.Enabled;
        OperationalState = InterfaceOperationalState.Down;

        // Ethernet-capable interfaces get a stable, locally-administered unicast MAC at creation
        // (see docs/architecture/ethernet-layer.md). Non-Ethernet media (Serial, Console) have no
        // MAC. Persistence restores the saved address via SetMacAddress rather than regenerating.
        if (Capabilities.HasFlag(InterfaceCapability.Ethernet))
        {
            MacAddress = NetSim.Core.Networking.MacAddress.CreateRandomUnicast();
        }
    }

    public EntityId Id { get; }

    /// <summary>
    /// The stable identifier of this interface within its device (e.g. "GigabitEthernet0/0").
    /// Unique per device, never changes for the life of the interface, and is what persistence
    /// and the connection system key on - display metadata lives in <see cref="Description"/> /
    /// <see cref="ShortName"/> instead.
    /// </summary>
    public string Name { get; }

    public InterfaceType InterfaceType { get; }

    // The device this interface belongs to. Has only "get", never "set" -
    // once created, an interface can never be moved to a different device.
    public NetworkDevice Device { get; }

    /// <summary>Nominal link speed. Defaults from <see cref="InterfaceType"/>; comparable, not a string.</summary>
    public InterfaceSpeed Speed { get; private set; }

    /// <summary>Which media families this interface can be cabled to - see <see cref="ConnectionCompatibility"/>.</summary>
    public InterfaceCapability Capabilities { get; }

    /// <summary>
    /// True when this interface participates in the Ethernet layer - its <see cref="Capabilities"/>
    /// include <see cref="InterfaceCapability.Ethernet"/>. Only such interfaces carry a
    /// <see cref="MacAddress"/> and can transmit an <see cref="Ethernet.EthernetFrame"/>.
    /// </summary>
    public bool SupportsEthernet => Capabilities.HasFlag(InterfaceCapability.Ethernet);

    /// <summary>
    /// The link-layer (MAC) address of this interface, or null for a non-Ethernet interface
    /// (<see cref="SupportsEthernet"/> is false - e.g. Serial, Console). An Ethernet-capable
    /// interface receives a stable, locally-administered unicast address when it is created; the
    /// address never changes for the life of the interface, and <see cref="SetMacAddress"/> only
    /// restores a persisted value on load. See docs/architecture/ethernet-layer.md.
    /// </summary>
    public MacAddress? MacAddress { get; private set; }

    /// <summary>Operator enable/disable, independent of link state - see <see cref="InterfaceAdministrativeState"/>.</summary>
    public InterfaceAdministrativeState AdministrativeState { get; private set; }

    public InterfaceOperationalState OperationalState { get; private set; }

    /// <summary>Optional free-text label for the UI (e.g. "Uplink to core"). Not an identifier.</summary>
    public string? Description { get; private set; }

    /// <summary>Compact form of <see cref="Name"/> for tooltips/canvas labels ("GigabitEthernet0/0" -> "G0/0").</summary>
    public string ShortName => InterfaceTypeInfo.ShortName(InterfaceType, Name);

    /// <summary>What the UI should show as the primary label: the description if one is set, otherwise the name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Name : Description!;

    // The "?" after Connection means this can be null - i.e. "not plugged into
    // anything yet". Every interface starts with no connection.
    public Connection? Connection { get; private set; }

    // A computed shortcut: true whenever Connection is not null.
    // Saves callers from writing "interface.Connection != null" everywhere.
    public bool IsConnected => Connection is not null;

    public bool IsEnabled => AdministrativeState == InterfaceAdministrativeState.Enabled;

    /// <summary>
    /// True when this interface can accept a new connection: it must be administratively enabled
    /// and not already carrying a connection. The connection system consults this rather than
    /// re-deriving the rule, and the UI uses it to dim unavailable anchors.
    /// </summary>
    public bool IsAvailable => IsEnabled && !IsConnected;

    /// <summary>True only when the interface is both administratively enabled and its link is up.</summary>
    public bool IsOperational => IsEnabled && OperationalState == InterfaceOperationalState.Up;

    public void SetDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// Overrides the nominal speed (e.g. a forced/negotiated rate, or a persisted value on
    /// rehydration). Speed is descriptive in this phase - it never affects compatibility.
    /// </summary>
    public void SetSpeed(InterfaceSpeed speed) => Speed = speed;

    /// <summary>
    /// Restores a specific MAC address - used only by persistence rehydration, so an interface
    /// comes back with the exact address it was saved with rather than a freshly generated one
    /// (see docs/architecture/ethernet-layer.md, "Persistence"). Ordinary code never calls this;
    /// the address is assigned once at creation and is otherwise immutable.
    /// </summary>
    public void SetMacAddress(MacAddress macAddress) => MacAddress = macAddress;

    // ---- IPv4 configuration (Phase 18) ----

    /// <summary>
    /// The interface's IPv4 address assignments, in the order they were added. Read-only - use
    /// <see cref="AddIPv4Configuration"/> / <see cref="SetPrimaryIPv4Configuration"/> /
    /// <see cref="RemoveIPv4Configuration"/> / <see cref="ClearIPv4Configuration"/> to change them.
    /// The layout is deliberately a list so secondary addresses are possible without a redesign;
    /// most interfaces will hold either zero or one entry. IPv4 has no MAC-style capability gate -
    /// any interface can be given an address (routers address serial links too).
    /// </summary>
    public IReadOnlyList<Ipv4InterfaceConfiguration> IPv4Configurations => _ipv4Configurations.AsReadOnly();

    /// <summary>The primary IPv4 configuration (the one flagged primary, else the first), or null when none is configured.</summary>
    public Ipv4InterfaceConfiguration? PrimaryIPv4Configuration =>
        _ipv4Configurations.FirstOrDefault(c => c.IsPrimary) ?? _ipv4Configurations.FirstOrDefault();

    /// <summary>The primary IPv4 address, or null when no IPv4 address is configured.</summary>
    public IPv4Address? IPv4Address => PrimaryIPv4Configuration?.Address;

    /// <summary>True when at least one IPv4 address is configured on this interface.</summary>
    public bool HasIPv4Configuration => _ipv4Configurations.Count > 0;

    /// <summary>
    /// Adds an IPv4 address assignment. The first address added is always the primary; any later
    /// one is stored as a secondary regardless of its own
    /// <see cref="Ipv4InterfaceConfiguration.IsPrimary"/> flag. Throws <see cref="DomainException"/>
    /// if the same <see cref="Ipv4InterfaceConfiguration.Address"/> is already configured.
    /// </summary>
    public void AddIPv4Configuration(Ipv4InterfaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (_ipv4Configurations.Any(c => c.Address == configuration.Address))
        {
            throw new DomainException(
                $"Interface '{Name}' on device '{Device.Name}' already has the IPv4 address {configuration.Address}.");
        }

        _ipv4Configurations.Add(_ipv4Configurations.Count == 0 ? configuration.AsPrimary() : configuration.AsSecondary());
    }

    /// <summary>
    /// Replaces every IPv4 configuration on the interface with a single primary
    /// <paramref name="configuration"/>. The common "set the interface's IP address" operation.
    /// </summary>
    public void SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _ipv4Configurations.Clear();
        _ipv4Configurations.Add(configuration.AsPrimary());
    }

    /// <summary>
    /// Removes the configuration for <paramref name="address"/>. If the primary was removed and
    /// other addresses remain, the first remaining one is promoted to primary. Returns false when
    /// the address was not configured.
    /// </summary>
    public bool RemoveIPv4Configuration(IPv4Address address)
    {
        var existing = _ipv4Configurations.FirstOrDefault(c => c.Address == address);
        if (existing is null)
        {
            return false;
        }

        _ipv4Configurations.Remove(existing);
        if (existing.IsPrimary && _ipv4Configurations.Count > 0)
        {
            _ipv4Configurations[0] = _ipv4Configurations[0].AsPrimary();
        }

        return true;
    }

    /// <summary>Removes every IPv4 address from the interface.</summary>
    public void ClearIPv4Configuration()
    {
        _ipv4Configurations.Clear();
        IPv4DefaultGateway = null;
    }

    /// <summary>
    /// The IPv4 default gateway configured on this interface (Phase 27): the next-hop router a host
    /// sends off-link traffic to. <c>null</c> when none is configured - the host can then only reach
    /// its own local subnet. A router does not need one (it makes routing decisions from its own
    /// table); it is the host-side counterpart of a router's default route. Not derived from any
    /// address - set explicitly, cleared automatically when the interface's IPv4 addressing is
    /// cleared.
    /// </summary>
    public IPv4Address? IPv4DefaultGateway { get; private set; }

    /// <summary>Sets the interface's IPv4 default gateway. Pass <c>null</c> to remove it.</summary>
    public void SetIPv4DefaultGateway(IPv4Address? gateway)
    {
        if (gateway is { } value && (value.IsUnspecified || value.IsLimitedBroadcast || value.IsMulticast))
        {
            throw new DomainException($"'{value}' is not a valid default gateway address.");
        }

        IPv4DefaultGateway = gateway;
    }

    // ---- IPv6 configuration (Phase 19) ----

    /// <summary>
    /// The interface's IPv6 address assignments, in the order they were added. Read-only - use
    /// <see cref="AddIPv6Configuration"/> / <see cref="SetPrimaryIPv6Configuration"/> /
    /// <see cref="RemoveIPv6Configuration"/> / <see cref="ClearIPv6Configuration"/> to change them.
    /// Independent of IPv4: an interface can carry both (dual stack). No capability gate and nothing
    /// auto-generated - an interface has no IPv6 address until one is configured.
    /// </summary>
    public IReadOnlyList<Ipv6InterfaceConfiguration> IPv6Configurations => _ipv6Configurations.AsReadOnly();

    /// <summary>The primary IPv6 configuration (the one flagged primary, else the first), or null when none is configured.</summary>
    public Ipv6InterfaceConfiguration? PrimaryIPv6Configuration =>
        _ipv6Configurations.FirstOrDefault(c => c.IsPrimary) ?? _ipv6Configurations.FirstOrDefault();

    /// <summary>The primary IPv6 address, or null when no IPv6 address is configured.</summary>
    public IPv6Address? IPv6Address => PrimaryIPv6Configuration?.Address;

    /// <summary>True when at least one IPv6 address is configured on this interface.</summary>
    public bool HasIPv6Configuration => _ipv6Configurations.Count > 0;

    /// <summary>
    /// Adds an IPv6 address assignment. The first address added is always the primary; any later
    /// one is stored as a secondary regardless of its own
    /// <see cref="Ipv6InterfaceConfiguration.IsPrimary"/> flag. Throws <see cref="DomainException"/>
    /// if the same <see cref="Ipv6InterfaceConfiguration.Address"/> is already configured.
    /// </summary>
    public void AddIPv6Configuration(Ipv6InterfaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (_ipv6Configurations.Any(c => c.Address == configuration.Address))
        {
            throw new DomainException(
                $"Interface '{Name}' on device '{Device.Name}' already has the IPv6 address {configuration.Address}.");
        }

        _ipv6Configurations.Add(_ipv6Configurations.Count == 0 ? configuration.AsPrimary() : configuration.AsSecondary());
    }

    /// <summary>
    /// Replaces every IPv6 configuration on the interface with a single primary
    /// <paramref name="configuration"/>.
    /// </summary>
    public void SetPrimaryIPv6Configuration(Ipv6InterfaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _ipv6Configurations.Clear();
        _ipv6Configurations.Add(configuration.AsPrimary());
    }

    /// <summary>
    /// Removes the configuration for <paramref name="address"/>. If the primary was removed and
    /// other addresses remain, the first remaining one is promoted to primary. Returns false when
    /// the address was not configured.
    /// </summary>
    public bool RemoveIPv6Configuration(IPv6Address address)
    {
        var existing = _ipv6Configurations.FirstOrDefault(c => c.Address == address);
        if (existing is null)
        {
            return false;
        }

        _ipv6Configurations.Remove(existing);
        if (existing.IsPrimary && _ipv6Configurations.Count > 0)
        {
            _ipv6Configurations[0] = _ipv6Configurations[0].AsPrimary();
        }

        return true;
    }

    /// <summary>Removes every IPv6 address from the interface.</summary>
    public void ClearIPv6Configuration() => _ipv6Configurations.Clear();

    // ---- ARP cache (Phase 20) ----

    /// <summary>
    /// This interface's ARP cache - the IPv4 -&gt; MAC mappings it has learned (or been configured
    /// with) on its own Layer 2 segment. Always present, empty until ARP traffic populates it.
    /// Scoped to this interface so a multi-interface device never mixes mappings across unrelated
    /// segments. Runtime state only - it is deliberately <em>not</em> persisted (see
    /// docs/architecture/arp-engine.md, "Persistence"). ARP is IPv4-only; IPv6 uses Neighbor
    /// Discovery in a later phase and never touches this cache.
    /// </summary>
    public ArpCache ArpCache => _arpCache;

    /// <summary>Administratively enables the interface. Link state is unchanged - enabling does not by itself bring a link up.</summary>
    public void Enable() => AdministrativeState = InterfaceAdministrativeState.Enabled;

    /// <summary>
    /// Administratively disables the interface. A disabled interface is forced operationally down
    /// and cannot be operational until it is enabled again. Any existing <see cref="Connection"/>
    /// is intentionally left attached (configuration survives an admin-down) - it simply stops
    /// being operational.
    /// </summary>
    public void Disable()
    {
        AdministrativeState = InterfaceAdministrativeState.Disabled;
        OperationalState = InterfaceOperationalState.Down;
    }

    public void BringUp()
    {
        if (!IsEnabled)
        {
            throw new DomainException(
                $"Interface '{Name}' on device '{Device.Name}' is administratively disabled and cannot be brought up.");
        }

        OperationalState = InterfaceOperationalState.Up;
    }

    public void BringDown()
    {
        OperationalState = InterfaceOperationalState.Down;
    }

    // "internal" = only Connection.cs (also inside Core) is allowed to call this.
    // Outside code cannot forcibly plug a connection into an interface - it has
    // to go through Connection.Create(), which calls this as part of its job.
    internal void AttachConnection(Connection connection)
    {
        if (Connection is not null)
        {
            throw new DomainException($"Interface '{Name}' on device '{Device.Name}' is already connected.");
        }

        Connection = connection;
    }

    internal void DetachConnection()
    {
        Connection = null;
    }
}

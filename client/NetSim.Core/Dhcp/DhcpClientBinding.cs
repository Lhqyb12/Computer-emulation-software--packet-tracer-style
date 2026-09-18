using NetSim.Core.Common;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The DHCP client runtime state for one interface (brief sections 9, 32, 46): whether the
/// interface is DHCP-managed at all (as opposed to statically configured), where it is in the
/// <see cref="DhcpClientState"/> machine, the configuration it last applied, the lease behind that
/// configuration, and the transaction id of the exchange in flight.
///
/// Mutable and owned by <see cref="IDhcpClientStateStore"/>; this is simulation/runtime state, not
/// part of the persisted device model (brief section 72), exactly like an <c>ArpCache</c>.
/// </summary>
public sealed class DhcpClientBinding
{
    internal DhcpClientBinding(EntityId interfaceId)
    {
        InterfaceId = interfaceId;
        State = DhcpClientState.Init;
    }

    public EntityId InterfaceId { get; }

    /// <summary>True once <c>IDhcpClient.Acquire</c> has been started for this interface - it is now DHCP-managed, not static (brief section 46).</summary>
    public bool IsDhcpManaged { get; set; }

    public DhcpClientState State { get; set; }

    /// <summary>The configuration currently applied to the interface by DHCP, or null when unbound.</summary>
    public DhcpNetworkConfiguration? Configuration { get; set; }

    /// <summary>The lease behind <see cref="Configuration"/>, or null.</summary>
    public DhcpLease? Lease { get; set; }

    /// <summary>The transaction id of the exchange in flight (or the last completed one).</summary>
    public uint? LastTransactionId { get; set; }

    /// <summary>True when a lease is held and bound.</summary>
    public bool IsBound => State == DhcpClientState.Bound && Configuration is not null;
}

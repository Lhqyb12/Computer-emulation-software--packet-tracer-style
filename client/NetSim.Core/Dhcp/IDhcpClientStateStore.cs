using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The per-interface DHCP client state table (brief sections 32 &amp; 46), host-scoped exactly like
/// <see cref="Udp.IUdpDeliveryManager"/> / <see cref="Dns.IDnsCache"/> rather than a field bolted
/// onto <see cref="NetworkInterface"/> (which is persisted - keeping DHCP state here means no change
/// to the persistence model). An interface with no entry here is statically configured.
/// </summary>
public interface IDhcpClientStateStore
{
    /// <summary>The interface's DHCP client state, creating an <see cref="DhcpClientState.Init"/> entry if there is none.</summary>
    DhcpClientBinding GetOrCreate(NetworkInterface networkInterface);

    bool TryGet(NetworkInterface networkInterface, [NotNullWhen(true)] out DhcpClientBinding? binding);

    bool TryGet(EntityId interfaceId, [NotNullWhen(true)] out DhcpClientBinding? binding);

    /// <summary>True when this interface is DHCP-managed (an entry exists and is flagged managed).</summary>
    bool IsDhcpManaged(NetworkInterface networkInterface);

    /// <summary>Forgets the interface's DHCP state entirely (e.g. the operator switched it back to static).</summary>
    bool Remove(NetworkInterface networkInterface);
}

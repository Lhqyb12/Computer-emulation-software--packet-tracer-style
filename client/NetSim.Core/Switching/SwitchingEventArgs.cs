using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// Payload for the <see cref="ISwitchingEngine"/> notifications (frame received, MAC learned /
/// moved / expired, forwarded, flooded, dropped). A plain data carrier following the same
/// Core-stays-dispatch-agnostic pattern as <see cref="EthernetFrameEventArgs"/> - the real
/// consumers (packet visualization, event timeline, monitoring, diagnostics) arrive in later
/// phases.
/// </summary>
public sealed class SwitchingEventArgs : EventArgs
{
    public SwitchingEventArgs(
        NetworkDevice switchDevice,
        EthernetFrame? frame = null,
        NetworkInterface? ingressPort = null,
        SwitchForwardingDecision? decision = null,
        IReadOnlyList<NetworkInterface>? egressPorts = null,
        MacAddress? mac = null,
        NetworkInterface? port = null,
        NetworkInterface? previousPort = null,
        PacketDropReason? dropReason = null,
        VlanId? vlan = null,
        string? detail = null)
    {
        Switch = switchDevice;
        Frame = frame;
        IngressPort = ingressPort;
        Decision = decision;
        EgressPorts = egressPorts ?? [];
        Mac = mac;
        Port = port;
        PreviousPort = previousPort;
        DropReason = dropReason;
        Vlan = vlan;
        Detail = detail;
    }

    public NetworkDevice Switch { get; }

    public EthernetFrame? Frame { get; }

    public NetworkInterface? IngressPort { get; }

    /// <summary>The VLAN the frame was classified into, when the event concerns a VLAN-scoped decision.</summary>
    public VlanId? Vlan { get; }

    public SwitchForwardingDecision? Decision { get; }

    public IReadOnlyList<NetworkInterface> EgressPorts { get; }

    /// <summary>The MAC address a learn / move / expire event concerns.</summary>
    public MacAddress? Mac { get; }

    /// <summary>The port a learn / move / expire event concerns (the new port for a move).</summary>
    public NetworkInterface? Port { get; }

    /// <summary>The port a moved MAC address used to be on.</summary>
    public NetworkInterface? PreviousPort { get; }

    /// <summary>The structured drop cause - set on the dropped event only.</summary>
    public PacketDropReason? DropReason { get; }

    public string? Detail { get; }
}

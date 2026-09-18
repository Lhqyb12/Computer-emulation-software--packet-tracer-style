using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Ethernet;

/// <summary>
/// Default <see cref="IEthernetTransmissionService"/>. Stateless apart from its event
/// subscribers; all packet bookkeeping is delegated to the injected <see cref="IPacketEngine"/>.
/// </summary>
public sealed class EthernetTransmissionService : IEthernetTransmissionService
{
    /// <summary>The protocol label used for the generic <see cref="Packet"/> that carries a frame.</summary>
    public const string EthernetProtocol = "Ethernet";

    private readonly IPacketEngine _packetEngine;

    public EthernetTransmissionService(IPacketEngine packetEngine)
    {
        ArgumentNullException.ThrowIfNull(packetEngine);
        _packetEngine = packetEngine;
    }

    public event EventHandler<EthernetFrameEventArgs>? FrameCreated;

    public event EventHandler<EthernetFrameEventArgs>? FrameTransmitted;

    public event EventHandler<EthernetFrameEventArgs>? FrameDelivered;

    public event EventHandler<EthernetFrameEventArgs>? FrameDropped;

    public EthernetFrame CreateFrame(MacAddress source, MacAddress destination, EtherType etherType, IPacketPayload? payload = null)
    {
        var frame = EthernetFrame.Create(source, destination, etherType, payload);
        FrameCreated?.Invoke(this, new EthernetFrameEventArgs(frame, detail: frame.ToString()));
        return frame;
    }

    public EthernetTransmissionResult Transmit(ITopologyView topology, NetworkInterface sourceInterface, EthernetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(sourceInterface);
        ArgumentNullException.ThrowIfNull(frame);

        // 1. The frame itself must be structurally sound (Create already guards the header; this
        //    also covers a broken encapsulated payload chain).
        var frameValidation = frame.Validate();
        if (!frameValidation.IsValid)
        {
            return Drop(frame, EthernetDropReasons.InvalidFrame, string.Join("; ", frameValidation.Errors), sourceInterface);
        }

        // 2. The source port must be an Ethernet port with an address.
        if (!sourceInterface.SupportsEthernet)
        {
            return Drop(frame, EthernetDropReasons.NotEthernetCapable,
                $"Source interface '{sourceInterface.Name}' on '{sourceInterface.Device.Name}' is not Ethernet-capable.", sourceInterface);
        }

        if (sourceInterface.MacAddress is null)
        {
            return Drop(frame, EthernetDropReasons.SourceMacMissing,
                $"Source interface '{sourceInterface.Name}' has no MAC address.", sourceInterface);
        }

        // 3. The source port must belong to the topology we are evaluating against.
        if (topology.GetInterface(sourceInterface.Id) is null)
        {
            return Drop(frame, EthernetDropReasons.SourceInterfaceNotInTopology,
                $"Source interface '{sourceInterface.Name}' on '{sourceInterface.Device.Name}' is not part of the topology.", sourceInterface);
        }

        // 4. The source port must be operational (admin-enabled and link up).
        if (!sourceInterface.IsOperational)
        {
            return Drop(frame, EthernetDropReasons.SourceInterfaceDown,
                $"Source interface '{sourceInterface.Name}' on '{sourceInterface.Device.Name}' is not operational.", sourceInterface);
        }

        // 5. There must be a cable.
        var connection = topology.GetConnection(sourceInterface);
        if (connection is null)
        {
            return Drop(frame, EthernetDropReasons.NoPhysicalConnection,
                $"Source interface '{sourceInterface.Name}' on '{sourceInterface.Device.Name}' is not connected.", sourceInterface);
        }

        // 6. Resolve the far endpoint and confirm it is still a live interface in the topology
        //    (guards against a removed device / removed connection race).
        NetworkInterface destinationInterface;
        try
        {
            destinationInterface = connection.GetOtherEndpoint(sourceInterface);
        }
        catch (DomainException ex)
        {
            return Drop(frame, EthernetDropReasons.InvalidEndpoint, ex.Message, sourceInterface);
        }

        if (topology.GetInterface(destinationInterface.Id) is null)
        {
            return Drop(frame, EthernetDropReasons.InvalidEndpoint,
                $"The far endpoint of '{sourceInterface.Name}' is not part of the topology.", sourceInterface);
        }

        // 7. The receiving port must also be an operational Ethernet port.
        if (!destinationInterface.SupportsEthernet)
        {
            return Drop(frame, EthernetDropReasons.NotEthernetCapable,
                $"Destination interface '{destinationInterface.Name}' on '{destinationInterface.Device.Name}' is not Ethernet-capable.",
                sourceInterface, destinationInterface);
        }

        if (!destinationInterface.IsOperational)
        {
            return Drop(frame, EthernetDropReasons.DestinationInterfaceDown,
                $"Destination interface '{destinationInterface.Name}' on '{destinationInterface.Device.Name}' is not operational.",
                sourceInterface, destinationInterface);
        }

        // 8. Success: hand the frame to the packet engine and drive one physical hop.
        var packet = _packetEngine.CreatePacket(sourceInterface.Device, destinationInterface.Device, EthernetProtocol, frame);

        _packetEngine.MarkTransmitted(packet, $"{sourceInterface.Device.Name}/{sourceInterface.Name}");
        FrameTransmitted?.Invoke(this, new EthernetFrameEventArgs(frame, sourceInterface, destinationInterface, packet, detail: frame.ToString()));

        _packetEngine.MarkInTransit(packet, connection.Id.ToString());

        var detail = DescribeDelivery(frame, destinationInterface);
        _packetEngine.MarkDelivered(packet, $"{destinationInterface.Device.Name}/{destinationInterface.Name}");
        FrameDelivered?.Invoke(this, new EthernetFrameEventArgs(frame, sourceInterface, destinationInterface, packet, detail: detail));

        return EthernetTransmissionResult.Delivered(sourceInterface, destinationInterface, packet, detail);
    }

    private EthernetTransmissionResult Drop(
        EthernetFrame frame,
        PacketDropReason reason,
        string detail,
        NetworkInterface? source,
        NetworkInterface? destination = null)
    {
        FrameDropped?.Invoke(this, new EthernetFrameEventArgs(frame, source, destination, dropReason: reason, detail: detail));
        return EthernetTransmissionResult.Dropped(reason, detail, source, destination);
    }

    private static string DescribeDelivery(EthernetFrame frame, NetworkInterface destination) => frame.DestinationKind switch
    {
        MacAddressKind.Broadcast => $"broadcast frame delivered to {destination.Device.Name}/{destination.Name}",
        MacAddressKind.Multicast => $"multicast frame ({frame.DestinationMac}) delivered to {destination.Device.Name}/{destination.Name}",
        _ => $"unicast frame ({frame.DestinationMac}) delivered to {destination.Device.Name}/{destination.Name}",
    };
}

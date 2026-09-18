using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;

namespace NetSim.Core.Arp;

/// <summary>
/// The ARP engine's service surface: it builds ARP requests / replies, bridges them to and from
/// the Ethernet layer (<see cref="EtherType.Arp"/> = 0x0806 - broadcast for a request, unicast for
/// a reply), performs the <em>cache-lookup-then-request</em> resolution flow against a
/// <see cref="NetworkInterface.ArpCache"/>, processes received ARP messages (learning the sender,
/// answering a request for an address the receiving interface owns) and raises ARP events for
/// later phases (visualization, timeline, monitoring, diagnostics, AI).
///
/// It is the ARP counterpart to <see cref="IP.IIPv4Layer"/> - it does <em>not</em> move frames
/// across cables (that stays the Ethernet layer's single physical hop) and it makes
/// <em>no routing decision</em>. ARP is a local-link protocol: a future routing layer decides
/// whether the target is on-link or is a next-hop router; this layer only ever resolves an address
/// it is given.
/// </summary>
public interface IArpLayer
{
    /// <summary>
    /// Builds an ARP request from <paramref name="senderInterface"/> for
    /// <paramref name="targetProtocolAddress"/> (sender MAC / IPv4 taken from the interface).
    /// Raises <see cref="RequestCreated"/>. Throws <see cref="Common.Exceptions.DomainException"/>
    /// if the interface has no MAC or no IPv4 address.
    /// </summary>
    ArpPacket CreateRequest(NetworkInterface senderInterface, IPv4Address targetProtocolAddress);

    /// <summary>
    /// Builds an ARP reply announcing <paramref name="senderProtocolAddress"/> is at
    /// <paramref name="senderInterface"/>'s MAC, addressed to
    /// <paramref name="targetHardwareAddress"/> / <paramref name="targetProtocolAddress"/>. Raises
    /// <see cref="ReplyCreated"/>.
    /// </summary>
    ArpPacket CreateReply(
        NetworkInterface senderInterface,
        IPv4Address senderProtocolAddress,
        MacAddress targetHardwareAddress,
        IPv4Address targetProtocolAddress);

    /// <summary>
    /// Wraps <paramref name="request"/> in a <em>broadcast</em> Ethernet frame (destination
    /// FF:FF:FF:FF:FF:FF, EtherType 0x0806) and raises <see cref="RequestEncapsulated"/>.
    /// </summary>
    EthernetFrame EncapsulateRequest(ArpPacket request);

    /// <summary>
    /// Wraps <paramref name="reply"/> in a <em>unicast</em> Ethernet frame addressed to the
    /// reply's target MAC (the requester) and raises <see cref="ReplyEncapsulated"/>.
    /// </summary>
    EthernetFrame EncapsulateReply(ArpPacket reply);

    /// <summary>Extracts the <see cref="ArpPacket"/> from an <see cref="EtherType.Arp"/> frame; false for any other frame.</summary>
    bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out ArpPacket? arp);

    /// <summary>
    /// The resolution entry point: checks <paramref name="senderInterface"/>'s ARP cache for
    /// <paramref name="targetProtocolAddress"/> first. A hit returns
    /// <see cref="ArpResolution.IsResolved"/> with the MAC and raises
    /// <see cref="ResolutionSucceeded"/> - <em>no request is generated</em>. A miss builds the
    /// broadcast ARP request (raising <see cref="RequestCreated"/> / <see cref="RequestEncapsulated"/>)
    /// and returns it via <see cref="ArpResolution.RequiresRequest"/>. Always raises
    /// <see cref="ResolutionStarted"/> first.
    /// </summary>
    ArpResolution Resolve(NetworkInterface senderInterface, IPv4Address targetProtocolAddress);

    /// <summary>
    /// Processes an ARP message that arrived on <paramref name="receivingInterface"/>: validates it,
    /// learns the sender's IPv4 -&gt; MAC mapping into that interface's cache (RFC 826 - a request
    /// teaches the sender too), and, for a request whose target IP the interface owns, generates the
    /// unicast reply. Returns a descriptive <see cref="ArpProcessingReport"/>. Raises the matching
    /// received / learned / updated / reply events.
    /// </summary>
    ArpProcessingReport HandleIncoming(NetworkInterface receivingInterface, EthernetFrame frame);

    /// <summary>As <see cref="HandleIncoming(NetworkInterface, EthernetFrame)"/> for an already-decapsulated message.</summary>
    ArpProcessingReport HandleIncoming(NetworkInterface receivingInterface, ArpPacket arp);

    /// <summary>Raised after an ARP request is built (<see cref="CreateRequest"/> or the miss path of <see cref="Resolve"/>).</summary>
    event EventHandler<ArpEventArgs>? RequestCreated;

    /// <summary>Raised after an ARP request is wrapped in its broadcast Ethernet frame.</summary>
    event EventHandler<ArpEventArgs>? RequestEncapsulated;

    /// <summary>Raised after an ARP reply is built.</summary>
    event EventHandler<ArpEventArgs>? ReplyCreated;

    /// <summary>Raised after an ARP reply is wrapped in its unicast Ethernet frame.</summary>
    event EventHandler<ArpEventArgs>? ReplyEncapsulated;

    /// <summary>Raised at the start of every <see cref="Resolve"/> call.</summary>
    event EventHandler<ArpEventArgs>? ResolutionStarted;

    /// <summary>Raised when <see cref="Resolve"/> is satisfied from the cache (a hit).</summary>
    event EventHandler<ArpEventArgs>? ResolutionSucceeded;

    /// <summary>Raised when <see cref="Resolve"/> cannot even build a request (interface has no MAC / IPv4).</summary>
    event EventHandler<ArpEventArgs>? ResolutionFailed;

    /// <summary>Raised when a valid ARP request is received.</summary>
    event EventHandler<ArpEventArgs>? RequestReceived;

    /// <summary>Raised when a valid ARP reply is received.</summary>
    event EventHandler<ArpEventArgs>? ReplyReceived;

    /// <summary>Raised when a received message adds a new mapping to a cache.</summary>
    event EventHandler<ArpEventArgs>? EntryLearned;

    /// <summary>Raised when a received message refreshes or changes an existing mapping.</summary>
    event EventHandler<ArpEventArgs>? EntryUpdated;

    /// <summary>Raised when a received message is dropped (not ARP, or structurally invalid).</summary>
    event EventHandler<ArpEventArgs>? PacketDropped;
}

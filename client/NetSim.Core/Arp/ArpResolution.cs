using NetSim.Core.Ethernet;
using NetSim.Core.Networking;

namespace NetSim.Core.Arp;

/// <summary>
/// The result of <see cref="IArpLayer.Resolve"/> - the "do I already know this MAC?" step that
/// runs <em>before</em> any ARP request is generated. Exactly one of three states:
/// <list type="bullet">
/// <item><see cref="IsResolved"/> - a cache hit; <see cref="HardwareAddress"/> / <see cref="Entry"/> are set and no request is needed.</item>
/// <item><see cref="RequiresRequest"/> - a cache miss; <see cref="Request"/> / <see cref="RequestFrame"/> carry the broadcast ARP request to send.</item>
/// <item><see cref="Failed"/> - the interface cannot even build a request (no MAC / no IPv4); <see cref="FailureReason"/> explains why.</item>
/// </list>
/// </summary>
public sealed class ArpResolution
{
    private ArpResolution(
        bool isResolved,
        bool failed,
        MacAddress? hardwareAddress,
        ArpCacheEntry? entry,
        ArpPacket? request,
        EthernetFrame? requestFrame,
        string? failureReason)
    {
        IsResolved = isResolved;
        HasFailed = failed;
        HardwareAddress = hardwareAddress;
        Entry = entry;
        Request = request;
        RequestFrame = requestFrame;
        FailureReason = failureReason;
    }

    /// <summary>True when the address was already in the ARP cache - no request is needed.</summary>
    public bool IsResolved { get; }

    /// <summary>True when a broadcast ARP request must be sent to resolve the address.</summary>
    public bool RequiresRequest => Request is not null;

    /// <summary>True when the sending interface is not usable for ARP (no MAC and/or no IPv4 address).</summary>
    public bool HasFailed { get; }

    /// <summary>The resolved MAC address - set only when <see cref="IsResolved"/>.</summary>
    public MacAddress? HardwareAddress { get; }

    /// <summary>The cache entry that satisfied the lookup - set only when <see cref="IsResolved"/>.</summary>
    public ArpCacheEntry? Entry { get; }

    /// <summary>The ARP request to broadcast - set only when <see cref="RequiresRequest"/>.</summary>
    public ArpPacket? Request { get; }

    /// <summary>
    /// The broadcast Ethernet frame carrying <see cref="Request"/> (destination
    /// FF:FF:FF:FF:FF:FF, EtherType 0x0806), ready to hand to the Ethernet transmission service -
    /// set only when <see cref="RequiresRequest"/>.
    /// </summary>
    public EthernetFrame? RequestFrame { get; }

    /// <summary>Why resolution failed - set only when <see cref="HasFailed"/>.</summary>
    public string? FailureReason { get; }

    internal static ArpResolution Resolved(MacAddress hardwareAddress, ArpCacheEntry entry) =>
        new(isResolved: true, failed: false, hardwareAddress, entry, request: null, requestFrame: null, failureReason: null);

    internal static ArpResolution Pending(ArpPacket request, EthernetFrame requestFrame) =>
        new(isResolved: false, failed: false, hardwareAddress: null, entry: null, request, requestFrame, failureReason: null);

    internal static ArpResolution Failed(string reason) =>
        new(isResolved: false, failed: true, hardwareAddress: null, entry: null, request: null, requestFrame: null, reason);
}

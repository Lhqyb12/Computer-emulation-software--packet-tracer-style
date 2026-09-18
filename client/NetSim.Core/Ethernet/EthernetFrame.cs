using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Vlans;

namespace NetSim.Core.Ethernet;

/// <summary>
/// A simulated Ethernet II frame. It is an <see cref="IPacketPayload"/> - the outermost payload
/// layer riding inside a generic <see cref="Packet"/> - so the Phase 16 packet engine carries and
/// tracks it with no Ethernet-specific changes. Its own <see cref="Payload"/> is the next layer
/// down (a <see cref="RawPayload"/> today; a future IPv4 packet or ARP message later), keyed by
/// <see cref="EtherType"/>.
///
/// The header (<see cref="DestinationMac"/>, <see cref="SourceMac"/>, an optional
/// <see cref="VlanTag"/>, <see cref="EtherType"/>) is kept logically separate from the payload.
/// This is a behavioural model, not a wire-format codec: there is no 14-byte serialisation and no
/// FCS/CRC - <see cref="Length"/> is a simulated size only (it does grow by 4 bytes when a
/// <see cref="VlanTag"/> is present, matching the 802.1Q tag width, so a later MTU check is
/// meaningful).
///
/// <para><b>VLAN tagging (Phase 26).</b> A frame between two end hosts and their access ports is
/// always untagged (<see cref="VlanTag"/> is <c>null</c>). The switching engine adds a tag with
/// <see cref="Tagged"/> when it sends a frame out of a trunk for a non-native VLAN, and strips it
/// with <see cref="Untagged"/> when it sends the frame toward an access port. The internal VLAN a
/// switch is forwarding in is <em>not</em> read from this field alone - see the switching engine -
/// but a tagged frame that crosses a trunk carries its VLAN here.</para>
/// </summary>
public sealed class EthernetFrame : IPacketPayload
{
    /// <summary>Simulated header size in bytes: 6 (dst) + 6 (src) + 2 (EtherType). No preamble/FCS.</summary>
    public const int HeaderSizeBytes = 14;

    /// <summary>Extra bytes an 802.1Q VLAN tag adds to the header (TPID + TCI).</summary>
    public const int VlanTagSizeBytes = 4;

    private EthernetFrame(
        MacAddress sourceMac, MacAddress destinationMac, EtherType etherType, IPacketPayload payload, VlanTag? vlanTag)
    {
        SourceMac = sourceMac;
        DestinationMac = destinationMac;
        EtherType = etherType;
        Payload = payload;
        VlanTag = vlanTag;
    }

    /// <summary>
    /// Builds a frame and rejects a structurally invalid header with a <see cref="DomainException"/>
    /// (the source must be a unicast address and the EtherType must be set) - an invalid frame never
    /// exists. The <paramref name="payload"/>'s own structural validity is the next layer's concern
    /// and is checked by <see cref="Validate"/> / the packet engine, not here; pass null for a
    /// payload-less frame. A frame created here is untagged; use <see cref="Tagged"/> to add a
    /// VLAN tag.
    /// </summary>
    public static EthernetFrame Create(
        MacAddress sourceMac,
        MacAddress destinationMac,
        EtherType etherType,
        IPacketPayload? payload = null,
        VlanTag? vlanTag = null)
    {
        if (!sourceMac.IsUnicast)
        {
            throw new DomainException($"Ethernet source MAC must be a unicast address, but was '{sourceMac}' ({sourceMac.Kind}).");
        }

        if (!etherType.IsSpecified)
        {
            throw new DomainException("Ethernet frame EtherType must be set.");
        }

        return new EthernetFrame(sourceMac, destinationMac, etherType, payload ?? RawPayload.Empty, vlanTag);
    }

    // ---- Header ----

    public MacAddress SourceMac { get; }

    public MacAddress DestinationMac { get; }

    public EtherType EtherType { get; }

    /// <summary>The 802.1Q VLAN tag, or null for an untagged frame (the common case).</summary>
    public VlanTag? VlanTag { get; }

    /// <summary>True when this frame carries an 802.1Q VLAN tag.</summary>
    public bool IsVlanTagged => VlanTag is not null;

    /// <summary>The tagged VLAN id, or null when the frame is untagged.</summary>
    public VlanId? VlanId => VlanTag?.VlanId;

    // ---- Payload ----

    /// <summary>The encapsulated next layer - never null (<see cref="RawPayload.Empty"/> when the frame carries nothing).</summary>
    public IPacketPayload Payload { get; }

    // ---- Destination classification (address-level only; no forwarding) ----

    public MacAddressKind DestinationKind => DestinationMac.Kind;

    public bool IsBroadcast => DestinationMac.IsBroadcast;

    public bool IsMulticast => DestinationMac.IsMulticast;

    public bool IsUnicast => DestinationMac.IsUnicast;

    // ---- VLAN tagging ----

    /// <summary>
    /// Returns a copy of this frame carrying <paramref name="tag"/> (same addresses, EtherType and
    /// payload). Frames are immutable, so the original is unchanged and can still be flooded out
    /// other ports untagged.
    /// </summary>
    public EthernetFrame Tagged(VlanTag tag) =>
        new(SourceMac, DestinationMac, EtherType, Payload, tag);

    /// <summary>
    /// Returns a copy of this frame with no VLAN tag. Returns <c>this</c> when the frame is already
    /// untagged.
    /// </summary>
    public EthernetFrame Untagged() =>
        VlanTag is null ? this : new EthernetFrame(SourceMac, DestinationMac, EtherType, Payload, vlanTag: null);

    // ---- IPacketPayload ----

    public string PayloadType => "Ethernet";

    public int Length => HeaderSizeBytes + (IsVlanTagged ? VlanTagSizeBytes : 0) + Payload.Length;

    public IPacketPayload? EncapsulatedPayload => Payload;

    /// <summary>
    /// Structural self-check: the source MAC is unicast, the EtherType is set, and the encapsulated
    /// payload chain is itself valid. No protocol semantics - the meaning of the payload for a
    /// given EtherType is a later phase's concern.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (!SourceMac.IsUnicast)
        {
            errors.Add($"Source MAC '{SourceMac}' is not a unicast address.");
        }

        if (!EtherType.IsSpecified)
        {
            errors.Add("EtherType is not set.");
        }

        var payloadResult = Payload.Validate();
        if (!payloadResult.IsValid)
        {
            errors.AddRange(payloadResult.Errors);
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() =>
        $"{SourceMac} -> {DestinationMac} [{EtherType.Name}]{(IsVlanTagged ? $" vlan {VlanTag!.Value.VlanId}" : string.Empty)} {Length}B";
}

using NetSim.Core.Common.Exceptions;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Arp;

/// <summary>
/// A simulated ARP message. Like <see cref="EthernetFrame"/> / <see cref="IP.IPv4Packet"/> it is
/// an <see cref="IPacketPayload"/>, so the Phase 16 packet engine carries and tracks it with no
/// ARP-specific changes, and it nests inside an Ethernet frame whose
/// <see cref="EtherType"/> is <see cref="EtherType.Arp"/> (0x0806). ARP encapsulates nothing, so
/// <see cref="EncapsulatedPayload"/> is always null.
///
/// The nine standard fields are all modelled (<see cref="HardwareType"/>,
/// <see cref="ProtocolType"/>, <see cref="HardwareAddressLength"/>,
/// <see cref="ProtocolAddressLength"/>, <see cref="Operation"/>,
/// <see cref="SenderHardwareAddress"/>, <see cref="SenderProtocolAddress"/>,
/// <see cref="TargetHardwareAddress"/>, <see cref="TargetProtocolAddress"/>). For the only
/// binding this phase supports - Ethernet + IPv4 - hardware type is <c>1</c>, protocol type is
/// <see cref="EtherType.IPv4"/>, and the address lengths are 6 and 4. Addresses use the existing
/// <see cref="MacAddress"/> / <see cref="IPv4Address"/> value objects, never strings. This is a
/// behavioural model, not a wire-format codec - <see cref="Length"/> is a simulated size only.
/// </summary>
public sealed class ArpPacket : IPacketPayload
{
    /// <summary>Simulated size of an Ethernet/IPv4 ARP message: htype(2)+ptype(2)+hlen(1)+plen(1)+oper(2)+sha(6)+spa(4)+tha(6)+tpa(4).</summary>
    public const int EthernetIPv4LengthBytes = 28;

    /// <summary>The hardware address length for Ethernet - 6 octets.</summary>
    public const byte EthernetHardwareAddressLength = 6;

    /// <summary>The protocol address length for IPv4 - 4 octets.</summary>
    public const byte IPv4ProtocolAddressLength = 4;

    private ArpPacket(
        ArpHardwareType hardwareType,
        EtherType protocolType,
        byte hardwareAddressLength,
        byte protocolAddressLength,
        ArpOperation operation,
        MacAddress senderHardwareAddress,
        IPv4Address senderProtocolAddress,
        MacAddress targetHardwareAddress,
        IPv4Address targetProtocolAddress)
    {
        HardwareType = hardwareType;
        ProtocolType = protocolType;
        HardwareAddressLength = hardwareAddressLength;
        ProtocolAddressLength = protocolAddressLength;
        Operation = operation;
        SenderHardwareAddress = senderHardwareAddress;
        SenderProtocolAddress = senderProtocolAddress;
        TargetHardwareAddress = targetHardwareAddress;
        TargetProtocolAddress = targetProtocolAddress;
    }

    // ---- Header fields ----

    public ArpHardwareType HardwareType { get; }

    public EtherType ProtocolType { get; }

    public byte HardwareAddressLength { get; }

    public byte ProtocolAddressLength { get; }

    public ArpOperation Operation { get; }

    public MacAddress SenderHardwareAddress { get; }

    public IPv4Address SenderProtocolAddress { get; }

    /// <summary>The target MAC. <see cref="MacAddress.Zero"/> in a normal request (not yet known).</summary>
    public MacAddress TargetHardwareAddress { get; }

    public IPv4Address TargetProtocolAddress { get; }

    public bool IsRequest => Operation == ArpOperation.Request;

    public bool IsReply => Operation == ArpOperation.Reply;

    // ---- Factories ----

    /// <summary>
    /// Builds an ARP request ("who has <paramref name="targetProtocolAddress"/>?"). The target MAC
    /// is <see cref="MacAddress.Zero"/> - it is what the request is trying to discover. Throws
    /// <see cref="DomainException"/> for a non-unicast sender MAC, an unset/non-host sender IPv4, or
    /// an unset target IPv4 - an invalid packet never exists.
    /// </summary>
    public static ArpPacket CreateRequest(
        MacAddress senderHardwareAddress, IPv4Address senderProtocolAddress, IPv4Address targetProtocolAddress)
    {
        GuardSender(senderHardwareAddress, senderProtocolAddress);

        if (targetProtocolAddress.IsUnspecified)
        {
            throw new DomainException("An ARP request's target IPv4 address must be set (not 0.0.0.0).");
        }

        return new ArpPacket(
            ArpHardwareType.Ethernet, EtherType.IPv4, EthernetHardwareAddressLength, IPv4ProtocolAddressLength,
            ArpOperation.Request, senderHardwareAddress, senderProtocolAddress, MacAddress.Zero, targetProtocolAddress);
    }

    /// <summary>
    /// Builds an ARP reply ("<paramref name="senderProtocolAddress"/> is at
    /// <paramref name="senderHardwareAddress"/>"), addressed to the requester
    /// (<paramref name="targetHardwareAddress"/> / <paramref name="targetProtocolAddress"/>). Throws
    /// <see cref="DomainException"/> for a non-unicast sender or target MAC, or an unset sender /
    /// target IPv4.
    /// </summary>
    public static ArpPacket CreateReply(
        MacAddress senderHardwareAddress,
        IPv4Address senderProtocolAddress,
        MacAddress targetHardwareAddress,
        IPv4Address targetProtocolAddress)
    {
        GuardSender(senderHardwareAddress, senderProtocolAddress);

        if (targetHardwareAddress.IsUnspecified || !targetHardwareAddress.IsUnicast)
        {
            throw new DomainException(
                $"An ARP reply's target MAC must be a unicast address, but was '{targetHardwareAddress}'.");
        }

        if (targetProtocolAddress.IsUnspecified)
        {
            throw new DomainException("An ARP reply's target IPv4 address must be set (not 0.0.0.0).");
        }

        return new ArpPacket(
            ArpHardwareType.Ethernet, EtherType.IPv4, EthernetHardwareAddressLength, IPv4ProtocolAddressLength,
            ArpOperation.Reply, senderHardwareAddress, senderProtocolAddress, targetHardwareAddress, targetProtocolAddress);
    }

    private static void GuardSender(MacAddress senderHardwareAddress, IPv4Address senderProtocolAddress)
    {
        if (senderHardwareAddress.IsUnspecified || !senderHardwareAddress.IsUnicast)
        {
            throw new DomainException(
                $"An ARP sender MAC must be a unicast address, but was '{senderHardwareAddress}'.");
        }

        if (senderProtocolAddress.IsUnspecified)
        {
            throw new DomainException("An ARP sender IPv4 address must be set (not 0.0.0.0).");
        }

        if (senderProtocolAddress.IsMulticast || senderProtocolAddress.IsLimitedBroadcast)
        {
            throw new DomainException(
                $"An ARP sender IPv4 address '{senderProtocolAddress}' is not a host address.");
        }
    }

    // ---- IPacketPayload ----

    public string PayloadType => "ARP";

    public int Length => EthernetIPv4LengthBytes;

    public IPacketPayload? EncapsulatedPayload => null;

    /// <summary>
    /// Structural self-check for an Ethernet + IPv4 ARP message: hardware type 1, protocol type
    /// IPv4, address lengths 6 / 4, a known operation, a unicast sender MAC, a set sender and target
    /// IPv4, and (for a reply) a unicast target MAC. A request's target MAC may legitimately be
    /// <see cref="MacAddress.Zero"/>. No protocol semantics - ownership of the target IP is the
    /// <c>ArpLayer</c>'s concern.
    /// </summary>
    public PacketValidationResult Validate()
    {
        var errors = new List<string>();

        if (!HardwareType.IsEthernet)
        {
            errors.Add($"Unexpected ARP hardware type {HardwareType.Value} (expected 1 / Ethernet).");
        }

        if (ProtocolType != EtherType.IPv4)
        {
            errors.Add($"Unexpected ARP protocol type {ProtocolType} (expected IPv4 / 0x0800).");
        }

        if (HardwareAddressLength != EthernetHardwareAddressLength)
        {
            errors.Add($"Unexpected ARP hardware address length {HardwareAddressLength} (expected 6).");
        }

        if (ProtocolAddressLength != IPv4ProtocolAddressLength)
        {
            errors.Add($"Unexpected ARP protocol address length {ProtocolAddressLength} (expected 4).");
        }

        if (Operation is not (ArpOperation.Request or ArpOperation.Reply))
        {
            errors.Add($"Unsupported ARP operation {(int)Operation}.");
        }

        if (SenderHardwareAddress.IsUnspecified || !SenderHardwareAddress.IsUnicast)
        {
            errors.Add($"ARP sender MAC '{SenderHardwareAddress}' is not a unicast address.");
        }

        if (SenderProtocolAddress.IsUnspecified)
        {
            errors.Add("ARP sender IPv4 address is not set.");
        }

        if (TargetProtocolAddress.IsUnspecified)
        {
            errors.Add("ARP target IPv4 address is not set.");
        }

        if (IsReply && (TargetHardwareAddress.IsUnspecified || !TargetHardwareAddress.IsUnicast))
        {
            errors.Add($"ARP reply target MAC '{TargetHardwareAddress}' is not a unicast address.");
        }

        return errors.Count == 0 ? PacketValidationResult.Valid : new PacketValidationResult(errors);
    }

    public override string ToString() => IsRequest
        ? $"ARP Request who-has {TargetProtocolAddress} tell {SenderProtocolAddress} ({SenderHardwareAddress})"
        : $"ARP Reply {SenderProtocolAddress} is-at {SenderHardwareAddress}";
}

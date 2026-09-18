using System.Text;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Packets;

/// <summary>
/// A leaf <see cref="IPacketPayload"/>: an opaque block of bytes with nothing encapsulated
/// inside it. This is the generic stand-in for "some data" so the engine never hardcodes a
/// string, an IPv4 packet or an ARP message as <em>the</em> payload type - a higher protocol
/// layer wraps its own <see cref="IPacketPayload"/> around one of these (or around another
/// protocol layer) when those phases arrive.
/// </summary>
public sealed class RawPayload : IPacketPayload
{
    private readonly byte[] _data;

    private RawPayload(byte[] data) => _data = data;

    /// <summary>The shared zero-length payload.</summary>
    public static RawPayload Empty { get; } = new([]);

    public static RawPayload FromBytes(ReadOnlySpan<byte> data) => data.IsEmpty ? Empty : new(data.ToArray());

    /// <summary>UTF-8 encodes <paramref name="text"/>. A convenience for tests and tooling - the engine itself never assumes a text payload.</summary>
    public static RawPayload FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? Empty : new(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>A payload of <paramref name="sizeInBytes"/> zero bytes - for size/MTU scenarios where the content does not matter.</summary>
    public static RawPayload OfSize(int sizeInBytes)
    {
        if (sizeInBytes < 0)
        {
            throw new DomainException($"Payload size cannot be negative (was {sizeInBytes}).");
        }

        return sizeInBytes == 0 ? Empty : new(new byte[sizeInBytes]);
    }

    public string PayloadType => "Raw";

    public int Length => _data.Length;

    public IPacketPayload? EncapsulatedPayload => null;

    /// <summary>Read-only view of the raw bytes.</summary>
    public ReadOnlyMemory<byte> Data => _data;

    public PacketValidationResult Validate() => PacketValidationResult.Valid;
}

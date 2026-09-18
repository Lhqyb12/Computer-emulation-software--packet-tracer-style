using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

/// <summary>
/// A configurable <see cref="IPacketPayload"/> for exercising encapsulation, length aggregation
/// and validation without any real protocol layer existing yet.
/// </summary>
internal sealed class StubPayload : IPacketPayload
{
    public string PayloadType { get; init; } = "Stub";

    public int OwnLength { get; init; }

    public IPacketPayload? EncapsulatedPayload { get; init; }

    public PacketValidationResult ValidationResult { get; init; } = PacketValidationResult.Valid;

    public int Length => OwnLength + (EncapsulatedPayload?.Length ?? 0);

    public PacketValidationResult Validate() => ValidationResult;
}

/// <summary>An <see cref="IPacketProcessor"/> that returns a preset result and records the packet it saw.</summary>
internal sealed class StubProcessor : IPacketProcessor
{
    private readonly PacketProcessingResult _result;

    public StubProcessor(PacketProcessingResult result) => _result = result;

    public int ProcessCallCount { get; private set; }

    public Packet? LastPacket { get; private set; }

    public PacketProcessingResult Process(Packet packet)
    {
        ProcessCallCount++;
        LastPacket = packet;
        return _result;
    }
}

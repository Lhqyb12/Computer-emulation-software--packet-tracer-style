namespace NetSim.Core.Tcp;

/// <summary>
/// Produces a TCP initial sequence number (ISN) for a new connection (brief section 13). This
/// simulation does not need a cryptographically-secure ISN algorithm like a real OS - it only needs
/// each connection to get a plausible, independent starting number, and tests need it to be
/// deterministic (an injectable generator, per the brief's suggestion).
/// </summary>
public interface IInitialSequenceNumberGenerator
{
    /// <summary>Returns the next initial sequence number. Each call is independent - a client and server connecting to each other get unrelated values.</summary>
    uint Next();
}

/// <summary>Default <see cref="IInitialSequenceNumberGenerator"/>: a uniformly random 32-bit value, close in spirit to a real (if not cryptographically secure) ISN.</summary>
public sealed class RandomInitialSequenceNumberGenerator : IInitialSequenceNumberGenerator
{
    private readonly Random _random;

    public RandomInitialSequenceNumberGenerator(Random? random = null) => _random = random ?? Random.Shared;

    public uint Next()
    {
        Span<byte> bytes = stackalloc byte[4];
        _random.NextBytes(bytes);
        return BitConverter.ToUInt32(bytes);
    }
}

/// <summary>
/// A deterministic <see cref="IInitialSequenceNumberGenerator"/> for tests: starts at
/// <paramref name="seed"/> and advances by <paramref name="step"/> on every call, so a test can
/// assert exact sequence numbers throughout a handshake instead of only their relative offsets.
/// </summary>
public sealed class SequentialInitialSequenceNumberGenerator(uint seed = 1000, uint step = 10000) : IInitialSequenceNumberGenerator
{
    private uint _next = seed;
    private readonly uint _step = step;

    public uint Next()
    {
        var value = _next;
        _next += _step;
        return value;
    }
}

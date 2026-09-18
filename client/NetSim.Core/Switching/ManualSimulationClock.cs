using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Switching;

/// <summary>
/// A hand-advanced <see cref="ISimulationClock"/> for tests: <see cref="Now"/> only ever changes
/// when <see cref="Advance"/> (or <see cref="AdvanceTo"/>) is called, so a test can learn a MAC
/// address, jump the clock past the aging time and assert the entry has expired without any real
/// waiting. Not registered for production use.
/// </summary>
public sealed class ManualSimulationClock : ISimulationClock
{
    public ManualSimulationClock(TimeSpan? start = null)
    {
        Now = start ?? TimeSpan.Zero;
        if (Now < TimeSpan.Zero)
        {
            throw new DomainException("A simulation clock cannot start before its origin.");
        }
    }

    public TimeSpan Now { get; private set; }

    /// <summary>Moves the clock forward by <paramref name="delta"/> (must be zero or positive).</summary>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new DomainException("A simulation clock cannot move backwards.");
        }

        Now += delta;
    }

    /// <summary>Moves the clock forward to <paramref name="instant"/> (must not be earlier than the current time).</summary>
    public void AdvanceTo(TimeSpan instant)
    {
        if (instant < Now)
        {
            throw new DomainException("A simulation clock cannot move backwards.");
        }

        Now = instant;
    }
}

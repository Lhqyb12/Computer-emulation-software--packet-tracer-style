using System.Diagnostics;

namespace NetSim.Core.Switching;

/// <summary>
/// The production <see cref="ISimulationClock"/>: <see cref="Now"/> is the time elapsed since the
/// instance was created, measured with the high-resolution monotonic process stopwatch (never the
/// system wall clock, so it is immune to clock adjustments). Dependency-free and thread-safe.
///
/// Until a real simulation scheduler exists this is the only moving clock in the app; it means a
/// dynamically learned MAC entry ages out roughly <see cref="MacAddressTable.AgingTime"/> of real
/// time after it was last seen, which is the closest honest behaviour available.
/// </summary>
public sealed class SystemSimulationClock : ISimulationClock
{
    private readonly long _origin = Stopwatch.GetTimestamp();

    public TimeSpan Now => Stopwatch.GetElapsedTime(_origin);
}

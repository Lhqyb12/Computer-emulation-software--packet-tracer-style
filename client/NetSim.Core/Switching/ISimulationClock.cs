namespace NetSim.Core.Switching;

/// <summary>
/// The simulator's notion of elapsed time, expressed as a monotonically non-decreasing
/// <see cref="Now"/> offset from some fixed origin. It exists so time-dependent behaviour - the
/// only one today is <see cref="MacAddressTable"/> aging - can be driven deterministically from a
/// test (<see cref="ManualSimulationClock"/>) instead of the wall clock.
///
/// There is no real-time / step simulation loop yet (see <c>Core.Simulation.ISimulationEngine</c>,
/// still a stub), so the production registration is <see cref="SystemSimulationClock"/> - a thin
/// monotonic wrapper over the process stopwatch. When a real scheduler arrives it becomes the
/// single source of simulated time and this interface does not change.
/// </summary>
public interface ISimulationClock
{
    /// <summary>Elapsed simulated time since the clock's origin. Never goes backwards.</summary>
    TimeSpan Now { get; }
}

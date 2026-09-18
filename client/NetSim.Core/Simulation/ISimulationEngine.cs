using NetSim.Core.Topology;

namespace NetSim.Core.Simulation;

/// <summary>
/// Minimal seam for the future simulation engine: something that operates on a
/// <see cref="Topology.Network"/>. Deliberately has no execution members yet (no
/// Start/Step/Stop) ג€” the actual simulation behavior belongs to a later phase.
/// </summary>
public interface ISimulationEngine
{
    Network Network { get; }
}

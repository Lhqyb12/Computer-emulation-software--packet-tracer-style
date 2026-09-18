using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

/// <summary>
/// Resolves a world-space click/hover to the id of the connection line under it, if any. Supplied
/// to <see cref="CanvasInteractionController"/> so that, in Select mode, clicking a cable selects
/// the <see cref="Core.Networking.Connection"/> it represents - reusing the exact same id-based
/// selection path devices already use, rather than a second selection system. Kept as a seam
/// (not a hard dependency) so the interaction controller stays free of connection geometry and
/// remains unit-testable on its own.
/// </summary>
public interface IConnectionHitTester
{
    EntityId? HitTestConnection(CanvasPoint worldPoint, double radius);
}

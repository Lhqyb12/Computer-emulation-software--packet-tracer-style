using NetSim.Core.Common;
using NetSim.Core.Networking;

namespace NetSim.Application.Canvas;

/// <summary>
/// A render-ready projection of one domain <see cref="Connection"/>: its id (so it can be
/// selected/deleted through the same id-based <see cref="ICanvasSelectionState"/> devices use)
/// and the two world-space anchor points its line currently runs between. Both anchors are
/// recomputed from the live device positions every time anything moves - there is no stored
/// line - so a connection always follows its endpoints (see docs/architecture/connections.md).
/// </summary>
public sealed record CanvasConnectionView(
    EntityId Id,
    CanvasPoint A,
    CanvasPoint B,
    ConnectionType ConnectionType,
    ConnectionState State,
    bool IsSelected);

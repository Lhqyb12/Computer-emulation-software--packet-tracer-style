using System;
using NetSim.Application.Common;
using NetSim.Core.Networking;

namespace NetSim.Application.Services;

/// <summary>
/// Coordinates connecting and disconnecting interfaces on the application's current
/// network. Domain rules (an interface cannot connect to itself or carry more than one
/// connection, both endpoints must belong to the current network) are enforced by
/// <see cref="Core.Topology.Network"/> and <see cref="Connection"/> themselves and
/// surface as <see cref="Core.Common.Exceptions.DomainException"/> ג€” this service does
/// not duplicate them.
/// </summary>
public interface IConnectionService
{
    OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.Copper);

    OperationResult Disconnect(Connection connection);

    /// <summary>
    /// Raised after a connection is successfully created or removed, so open UI that reflects
    /// interface connection state (e.g. the Device Properties panel) can refresh without the
    /// canvas selection having to change.
    /// </summary>
    event EventHandler? ConnectionsChanged;
}

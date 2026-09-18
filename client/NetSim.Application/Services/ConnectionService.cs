using System;
using NetSim.Application.Common;
using NetSim.Application.State;
using NetSim.Core.Networking;

namespace NetSim.Application.Services;

public sealed class ConnectionService : IConnectionService
{
    private readonly IApplicationState _applicationState;

    public ConnectionService(IApplicationState applicationState)
    {
        _applicationState = applicationState;
    }

    public event EventHandler? ConnectionsChanged;

    public OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.Copper)
    {
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, "No active network. Create a network before connecting interfaces.");
        }

        // Workflow rule (not a domain invariant - see Connection.Create): a new connection must
        // not be made to an administratively-disabled interface. The domain still allows a cable
        // on a shut port so a saved topology rehydrates, but the app refuses to create one.
        if (!endpointA.IsEnabled)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Interface {endpointA.Name} is administratively disabled.");
        }

        if (!endpointB.IsEnabled)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Interface {endpointB.Name} is administratively disabled.");
        }

        // Network.Connect() already enforces every domain rule that matters here
        // (self-connection, endpoint already connected, incompatible media, duplicate,
        // endpoint not part of this network) by throwing DomainException - nothing to duplicate.
        var connection = network.Connect(endpointA, endpointB, connectionType);

        // Creating a connection is a topology edit - the project now has unsaved changes.
        // Goes through the same IApplicationState door every other edit uses (no-op when no
        // project is open, e.g. in unit tests).
        _applicationState.MarkCurrentProjectDirty();
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult<Connection>.Success(connection);
    }

    public OperationResult Disconnect(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.RemoveConnection(connection))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "Connection was not found in the current network.");
        }

        _applicationState.MarkCurrentProjectDirty();
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }
}

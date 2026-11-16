using System;                          // EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;      // IReadOnlyList<T>
using System.Linq;                     // Where, ToList
using NetSim.Application.Common;       // OperationResult, OperationErrorType
using NetSim.Application.State;        // IApplicationState - the open network, and "unsaved changes"
using NetSim.Core.Common.Exceptions;   // DomainException - a Core rule was broken
using NetSim.Core.Devices;             // NetworkDevice
using NetSim.Core.Networking;          // NetworkInterface, Connection, ConnectionType

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of IConnectionService.
// Changes from the reference project:
//   - A broken cable rule comes back as a failure answer instead of an exception, so the cable tool
//     (step 6) can just show the message.
//   - GetConnectionsOf() was added: "what is connected to this device".
// "sealed" = no other class can inherit from it
public sealed class ConnectionService : IConnectionService
{
    private readonly IApplicationState _applicationState;   // the open network, and "unsaved changes"

    // Purpose:       Builds the service.
    // Precondition:  applicationState is not null (the DI container passes it in).
    // Postcondition: the service is ready to use.
    public ConnectionService(IApplicationState applicationState)
    {
        // Stop right away with a clear message if nothing was passed in
        ArgumentNullException.ThrowIfNull(applicationState);

        _applicationState = applicationState;   // keep the open-network state
    }

    // Raised after a cable was added or removed
    public event EventHandler? ConnectionsChanged;

    // Purpose:       Connects two ports with a cable.
    // Precondition:  a project is open; both ports are in its network and turned on.
    // Postcondition: success - cable in the network + project "unsaved" + ConnectionsChanged;
    //                failure - nothing changed.
    public OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.Copper)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        // No open network = no project is open
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, "No project is open. Open or create a project before connecting devices.");
        }

        // A new cable may not go into a port the user turned off. (Core itself allows it, so a saved
        // network with such a cable can still be loaded - but a NEW cable like that is refused here)
        if (!endpointA.IsEnabled)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Port {endpointA.Name} on {endpointA.Device.Name} is turned off.");
        }

        // The same check for the second port
        if (!endpointB.IsEnabled)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Port {endpointB.Name} on {endpointB.Device.Name} is turned off.");
        }

        // The cable itself. "try" = run it, and if a Core rule is broken, jump to "catch"
        Connection connection;
        try
        {
            // Network.Connect checks every cable rule and throws DomainException when one is broken
            connection = network.Connect(endpointA, endpointB, connectionType);
        }
        catch (DomainException ex)
        {
            // A rule was broken (e.g. the port already has a cable) - nothing was changed;
            // return Core's own message, so the user sees exactly why
            return OperationResult<Connection>.Failure(OperationErrorType.Conflict, ex.Message);
        }

        _applicationState.MarkCurrentProjectDirty();         // the project now has a change that was not saved
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);   // tell the listeners ("?." = only if someone listens)

        // Success, with the new cable
        return OperationResult<Connection>.Success(connection);
    }

    // Purpose:       Removes a cable.
    // Precondition:  a project is open and the cable is in its network.
    // Postcondition: success - cable gone + project "unsaved" + ConnectionsChanged; failure - nothing changed.
    public OperationResult Disconnect(Connection connection)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(connection);

        // No open network = no project is open
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is open.");
        }

        // Remove it. false = this cable is not in the network (e.g. it was already removed)
        if (!network.RemoveConnection(connection))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "This cable was not found in the open network.");
        }

        _applicationState.MarkCurrentProjectDirty();         // the project now has a change that was not saved
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);   // tell the listeners

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose:       Lists the cables that have one end on a device.
    // Precondition:  none.
    // Postcondition: returns those cables, or an empty list. Nothing is changed.
    public IReadOnlyList<Connection> GetConnectionsOf(NetworkDevice device)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(device);

        // No project open - no cables at all. "[]" = an empty list
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return [];
        }

        // Every cable whose first OR second end is a port of this device
        return network.Connections
            .Where(c => ReferenceEquals(c.EndpointA.Device, device) || ReferenceEquals(c.EndpointB.Device, device))   // keep only this device's cables
            .ToList();                                                                                                  // into a new list
    }
}

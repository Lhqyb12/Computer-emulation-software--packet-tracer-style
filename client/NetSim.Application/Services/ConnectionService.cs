using System;                          // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;      // brings in IReadOnlyList<T>
using System.Linq;                     // brings in Where (keep only the items that pass a test) and ToList (copy into a new list)
using NetSim.Application.Common;       // brings in OperationResult and OperationErrorType - success / failure answers
using NetSim.Application.State;        // brings in IApplicationState - the open network, and the "unsaved changes" flag
using NetSim.Core.Common.Exceptions;   // brings in DomainException - the error Core throws when one of its rules is broken
using NetSim.Core.Devices;             // brings in NetworkDevice - a device
using NetSim.Core.Networking;          // brings in NetworkInterface, Connection, ConnectionType

namespace NetSim.Application.Services;   // the "address" of this file: the Services part of the Application layer

// The class that does the work of IConnectionService.
// Changes from the reference project:
//   - A broken cable rule comes back as a failure answer instead of an exception, so the cable tool
//     (step 6) can just show the message.
//   - GetConnectionsOf() was added: "what is connected to this device".
// "sealed" = no other class can inherit from it; ": IConnectionService" = it does everything the interface lists
public sealed class ConnectionService : IConnectionService
{
    // The open network, and the "unsaved changes" flag. Set once in the constructor, never replaced
    private readonly IApplicationState _applicationState;

    // Purpose:       Builds the service.
    // Precondition:  receives applicationState - the app's "what is open" state (passed in by the DI container; not null).
    // Postcondition: returns nothing (a constructor) - the service is ready to use.
    public ConnectionService(IApplicationState applicationState)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);

        _applicationState = applicationState;   // keep the open-network state, to use in every method
    }

    // The event raised after a cable was added or removed. "?" = may have no listeners
    public event EventHandler? ConnectionsChanged;

    // Purpose:       Connects two ports with a cable.
    // Precondition:  receives endpointA, endpointB - the two ports (in the open network, both turned on);
    //                connectionType - the cable kind (straight-through copper when not given).
    // Postcondition: returns success + the new cable (network + "unsaved" + ConnectionsChanged), or failure + why (nothing changed).
    public OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.CopperStraightThrough)
    {
        // If either port is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        // "is not { } network" = if there is no open network -> go inside; otherwise keep it in "network".
        // No network = no project is open
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, "No project is open. Open or create a project before connecting devices.");
        }

        // A NEW cable may not go into a port the user turned off. (Core itself allows a cable on a turned-off port, so a
        // saved network with such a cable can still be loaded - but making a NEW one like that is refused here.)
        // "!" = NOT: if the first port is not enabled...
        if (!endpointA.IsEnabled)
        {
            // ...say which port and which device, e.g. "Port Ethernet0 on PC0 is turned off."
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Port {endpointA.Name} on {endpointA.Device.Name} is turned off.");
        }

        // The same check for the second port
        if (!endpointB.IsEnabled)
        {
            return OperationResult<Connection>.Failure(OperationErrorType.InvalidState, $"Port {endpointB.Name} on {endpointB.Device.Name} is turned off.");
        }

        // The new cable will be kept here. It is declared before the "try" so it can be used after it
        Connection connection;

        // "try" = run this block; if anything inside throws a DomainException, jump to the "catch" block below
        try
        {
            // Network.Connect checks every cable rule (both devices in the network, not the same pair twice, and through
            // Connection.Create: not to itself, ports free, the kinds fit, the cable kind fits both ports) and throws
            // DomainException when one is broken. If all pass, it makes the cable and decides if its link is up (step 7)
            connection = network.Connect(endpointA, endpointB, connectionType);
        }
        catch (DomainException ex)
        {
            // A rule was broken (e.g. the port already has a cable) - nothing was changed. Hand back a failure with
            // Core's OWN message (ex.Message), so the user sees exactly why. Conflict = "it clashes with what exists"
            return OperationResult<Connection>.Failure(OperationErrorType.Conflict, ex.Message);
        }

        _applicationState.MarkCurrentProjectDirty();         // the project now has a change that was not saved
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);   // tell the listeners ("?." = only if someone listens; "this" = who raised it)

        // Hand back a success, with the new cable inside it
        return OperationResult<Connection>.Success(connection);
    }

    // Purpose:       Removes a cable.
    // Precondition:  receives connection - the cable (in the open network).
    // Postcondition: returns success (cable gone + "unsaved" + ConnectionsChanged), or failure + why (nothing changed).
    public OperationResult Disconnect(Connection connection)
    {
        // If no cable was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(connection);

        // No open network = no project is open
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is open.");
        }

        // Remove it. Network.RemoveConnection unplugs it from both ports and takes it out of the network; it returns
        // false when this cable is not in the network (e.g. it was already removed). "!" = NOT
        if (!network.RemoveConnection(connection))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "This cable was not found in the open network.");
        }

        _applicationState.MarkCurrentProjectDirty();         // the project now has a change that was not saved
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);   // tell the listeners

        // Hand back a success
        return OperationResult.Success();
    }

    // Purpose:       Lists the cables that have one end on a device.
    // Precondition:  receives device - the device (not null).
    // Postcondition: returns those cables in a new list, or an empty list. Nothing is changed.
    public IReadOnlyList<Connection> GetConnectionsOf(NetworkDevice device)
    {
        // If no device was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(device);

        // No project open - no cables at all. "[]" = an empty list
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return [];
        }

        // Every cable of the network...
        return network.Connections
            // ...keep only those whose first end OR ("||") second end is a port of this device.
            // c = one cable; c.EndpointA.Device = the device its first port belongs to; ReferenceEquals = the very same object
            .Where(c => ReferenceEquals(c.EndpointA.Device, device) || ReferenceEquals(c.EndpointB.Device, device))
            // ...and copy them into a new list (so the caller gets a fixed answer)
            .ToList();
    }
}

using System;                      // EventHandler
using System.Collections.Generic;  // IReadOnlyList<T>
using NetSim.Application.Common;   // OperationResult - success / failure answers
using NetSim.Core.Devices;         // NetworkDevice
using NetSim.Core.Networking;      // NetworkInterface, Connection, ConnectionType

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of cables: what is connected to what in the open project.
// The cable rules themselves (not to itself, port already taken, cable kinds that don't fit) live in
// Core (Network.Connect and Connection.Create); this service does not repeat them - it turns them
// into a failure answer the screen can show, and adds the rule "not to a port that is turned off".
// An interface = a list of what the class can do, without how
public interface IConnectionService
{
    // Raised after a cable was added or removed through this service, so the screens can refresh
    event EventHandler? ConnectionsChanged;

    // Purpose:       Connects two ports with a cable.
    // Precondition:  a project is open; both ports belong to devices in its network; both are turned on.
    // Postcondition: on success - the cable is in the network and plugged into both ports, the project is
    //                marked "unsaved" and ConnectionsChanged was raised. On failure - nothing changed
    //                (InvalidState: no project / a port is turned off; Conflict: a cable rule was broken).
    OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.Copper);

    // Purpose:       Removes a cable.
    // Precondition:  a project is open and the cable is in its network.
    // Postcondition: on success - the cable is unplugged from both ports and gone, the project is marked
    //                "unsaved" and ConnectionsChanged was raised. On failure (InvalidState / NotFound) - nothing changed.
    OperationResult Disconnect(Connection connection);

    // Purpose:       Lists the cables that have one end on a device.
    // Precondition:  none.
    // Postcondition: returns those cables (an empty list when no project is open or the device has none).
    //                Nothing is changed.
    IReadOnlyList<Connection> GetConnectionsOf(NetworkDevice device);
}

using System;                      // brings in EventHandler - the type of an event
using System.Collections.Generic;  // brings in IReadOnlyList<T> - a list that can only be read
using NetSim.Application.Common;   // brings in OperationResult - a "success / failure + why" answer
using NetSim.Core.Devices;         // brings in NetworkDevice - a device
using NetSim.Core.Networking;      // brings in NetworkInterface (a port), Connection (a cable), ConnectionType (its kind)

namespace NetSim.Application.Services;   // the "address" of this file: the Services part of the Application layer

// The rules of cables: what is connected to what in the open project.
// The cable rules themselves (not to itself, port already taken, the cable kind must fit the ports) live in Core
// (Network.Connect, Connection.Create, CableRules); this service does not repeat them - it turns them into a
// failure answer the screen can show, and adds one rule of its own: "not into a port that is turned off".
// "interface" = a list of WHAT the class can do, without HOW (ConnectionService does the work)
public interface IConnectionService
{
    // Raised after a cable was added or removed through this service, so the screens can refresh
    event EventHandler? ConnectionsChanged;

    // Purpose:       Connects two ports with a cable.
    // Precondition:  receives endpointA, endpointB - the two ports (their devices must be in the open network, and both
    //                ports must be turned on); connectionType - the cable kind (straight-through copper when not given).
    // Postcondition: returns an OperationResult<Connection>: success + the new cable (it is in the network and plugged
    //                into both ports, the project is marked "unsaved", ConnectionsChanged was raised), or failure + why
    //                (InvalidState: no project / a port is turned off; Conflict: a cable rule was broken) - then nothing changed.
    OperationResult<Connection> Connect(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType = ConnectionType.CopperStraightThrough);

    // Purpose:       Removes a cable.
    // Precondition:  receives connection - the cable (it must be in the open network).
    // Postcondition: returns an OperationResult: success (the cable is unplugged from both ports and gone, the project is
    //                marked "unsaved", ConnectionsChanged was raised), or failure + why (InvalidState / NotFound).
    OperationResult Disconnect(Connection connection);

    // Purpose:       Lists the cables that have one end on a device.
    // Precondition:  receives device - the device.
    // Postcondition: returns those cables (an empty list when no project is open or the device has none). Nothing is changed.
    IReadOnlyList<Connection> GetConnectionsOf(NetworkDevice device);
}

using NetSim.Core.Common;              // EntityId
using NetSim.Core.Common.Exceptions;   // DomainException

namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A cable between exactly two ports (on two devices).
// Made only through Connection.Create, which checks all the rules first.
public sealed class Connection
{
    // Purpose: Builds the cable object. Private: nobody can write "new Connection(...)" -
    //          the only way in is Create below, after the checks.
    // Input:   endpointA, endpointB - the two ports; connectionType - what the cable is made of.
    // Output:  none (a constructor).
    private Connection(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType)
    {
        Id = EntityId.New();               // a new unique id
        EndpointA = endpointA;             // the first port
        EndpointB = endpointB;             // the second port
        ConnectionType = connectionType;   // the cable material
        State = ConnectionState.Down;      // a new cable starts not working
    }

    // The cable's unique id
    public EntityId Id { get; }

    // The port at one end
    public NetworkInterface EndpointA { get; }

    // The port at the other end
    public NetworkInterface EndpointB { get; }

    // What the cable is made of
    public ConnectionType ConnectionType { get; }

    // Is the cable working
    public ConnectionState State { get; private set; }

    // Purpose: Creates a cable between two ports - the only way to make one.
    // Input:   endpointA, endpointB - the two ports;
    //          connectionType - the material; "= ConnectionType.Copper" = Copper when the caller does not say.
    // Output:  the new cable, already plugged into both ports.
    //          Throws DomainException when a rule is broken (see the 4 rules below).
    public static Connection Create(
        NetworkInterface endpointA,
        NetworkInterface endpointB,
        ConnectionType connectionType = ConnectionType.Copper)
    {
        // null is not allowed for either port
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        // Rule 1: a port can't be cabled to itself. "ReferenceEquals" = the very same object
        if (ReferenceEquals(endpointA, endpointB))
        {
            throw new DomainException("An interface cannot be connected to itself.");
        }

        // Rule 2: the first port must be free
        if (endpointA.IsConnected)
        {
            throw new DomainException(
                $"Interface '{endpointA.Name}' on device '{endpointA.Device.Name}' is already connected.");
        }

        // Rule 3: the second port must be free
        if (endpointB.IsConnected)
        {
            throw new DomainException(
                $"Interface '{endpointB.Name}' on device '{endpointB.Device.Name}' is already connected.");
        }

        // Rule 4: the two port kinds must take the same kind of cable (e.g. Ethernet with Ethernet).
        // "out var" = the method fills the reason when the answer is "no"
        if (!ConnectionCompatibility.AreCompatible(endpointA.InterfaceType, endpointB.InterfaceType, out var incompatibilityReason))
        {
            // "!" = we know the reason is not null when the answer is "no"
            throw new DomainException(incompatibilityReason!);
        }

        // All rules passed - build the cable
        var connection = new Connection(endpointA, endpointB, connectionType);

        // Plug it into both ports
        endpointA.AttachConnection(connection);
        endpointB.AttachConnection(connection);

        // Hand the new cable back
        return connection;
    }

    // Purpose: Given one end of the cable, finds the port at the other end.
    // Input:   endpoint - one of the two ports.
    // Output:  the other port. Throws DomainException when the given port is not on this cable.
    public NetworkInterface GetOtherEndpoint(NetworkInterface endpoint)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(endpoint);

        // Given A - return B
        if (ReferenceEquals(endpoint, EndpointA))
        {
            return EndpointB;
        }

        // Given B - return A
        if (ReferenceEquals(endpoint, EndpointB))
        {
            return EndpointA;
        }

        // Neither - the port is not on this cable
        throw new DomainException("The given interface is not an endpoint of this connection.");
    }

    // Purpose: Marks the cable as working.
    // Input:   nothing.
    // Output:  nothing.
    public void BringUp()
    {
        State = ConnectionState.Up;
    }

    // Purpose: Marks the cable as not working.
    // Input:   nothing.
    // Output:  nothing.
    public void BringDown()
    {
        State = ConnectionState.Down;
    }

    // Purpose: Unplugs the cable from both ports. After this both ports are free again.
    // Input:   nothing.
    // Output:  nothing.
    public void Disconnect()
    {
        EndpointA.DetachConnection();   // free the first port
        EndpointB.DetachConnection();   // free the second port
    }
}

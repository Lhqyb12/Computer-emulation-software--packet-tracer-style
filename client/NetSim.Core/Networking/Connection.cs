using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// An explicit, strongly-typed link between exactly two <see cref="NetworkInterface"/>
/// endpoints. Connections are created through <see cref="Create"/> rather than a public
/// constructor so that the "an interface can carry at most one connection" and
/// "an interface cannot connect to itself" rules always hold.
/// </summary>
public sealed class Connection
{
    // "private" constructor = nobody, not even other classes in this same
    // project, can write "new Connection(...)". The ONLY way to build one is
    // the static Create(...) factory method below, which runs all the safety
    // checks first.
    private Connection(NetworkInterface endpointA, NetworkInterface endpointB, ConnectionType connectionType)
    {
        Id = EntityId.New();
        EndpointA = endpointA;
        EndpointB = endpointB;
        ConnectionType = connectionType;
        State = ConnectionState.Down;
    }

    public EntityId Id { get; }

    public NetworkInterface EndpointA { get; }

    public NetworkInterface EndpointB { get; }

    public ConnectionType ConnectionType { get; }

    public ConnectionState State { get; private set; }

    // The one and only door for creating a Connection. "= ConnectionType.Copper"
    // is a default value - if the caller doesn't specify a type, Copper is used
    // automatically, so most call sites can just pass the two interfaces.
    public static Connection Create(
        NetworkInterface endpointA,
        NetworkInterface endpointB,
        ConnectionType connectionType = ConnectionType.Copper)
    {
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        // Rule 1: can't connect an interface to itself.
        if (ReferenceEquals(endpointA, endpointB))
        {
            throw new DomainException("An interface cannot be connected to itself.");
        }

        // Rule 2 & 3: neither side can already have a cable plugged in -
        // an interface can only ever be part of one Connection at a time.
        if (endpointA.IsConnected)
        {
            throw new DomainException(
                $"Interface '{endpointA.Name}' on device '{endpointA.Device.Name}' is already connected.");
        }

        if (endpointB.IsConnected)
        {
            throw new DomainException(
                $"Interface '{endpointB.Name}' on device '{endpointB.Device.Name}' is already connected.");
        }

        // Rule 4: the two interfaces' media capabilities must overlap (Ethernet<->Ethernet,
        // Serial<->Serial, ...). This is a physical-layer (L1) check only - a cable can be run to
        // an administratively-disabled port, it simply will not be operational; refusing to wire
        // up a shut port is a workflow rule enforced by the Connect tool / IConnectionService, not
        // a domain invariant. The rule set lives in ConnectionCompatibility so it can grow without
        // this factory changing.
        if (!ConnectionCompatibility.AreCompatible(endpointA.InterfaceType, endpointB.InterfaceType, out var incompatibilityReason))
        {
            throw new DomainException(incompatibilityReason!);
        }

        // All checks passed - now it's safe to actually build the connection
        // and tell both interfaces about it.
        var connection = new Connection(endpointA, endpointB, connectionType);
        endpointA.AttachConnection(connection);
        endpointB.AttachConnection(connection);
        return connection;
    }

    public NetworkInterface GetOtherEndpoint(NetworkInterface endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (ReferenceEquals(endpoint, EndpointA))
        {
            return EndpointB;
        }

        if (ReferenceEquals(endpoint, EndpointB))
        {
            return EndpointA;
        }

        throw new DomainException("The given interface is not an endpoint of this connection.");
    }

    public void BringUp()
    {
        State = ConnectionState.Up;
    }

    public void BringDown()
    {
        State = ConnectionState.Down;
    }

    // Unplugs both sides at once - after this, both interfaces go back to
    // IsConnected == false, and this Connection object is no longer attached
    // to anything (though the object itself still exists until nobody holds
    // a reference to it - garbage collection cleans it up automatically).
    public void Disconnect()
    {
        EndpointA.DetachConnection();
        EndpointB.DetachConnection();
    }
}

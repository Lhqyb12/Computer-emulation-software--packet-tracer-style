namespace NetSim.Core.Dhcp;

/// <summary>
/// The DHCP message type carried in option 53 (brief section 25). The real RFC 2132 numbers are
/// used purely for familiarity - nothing decodes a wire format. The architecture allows further
/// types (INFORM, FORCERENEW, ...) to be added without touching the client/server switch beyond a
/// new case.
/// </summary>
public enum DhcpMessageType : byte
{
    /// <summary>Client broadcast to locate available servers.</summary>
    Discover = 1,

    /// <summary>Server to client in response to DISCOVER, with an offered configuration.</summary>
    Offer = 2,

    /// <summary>Client to server(s): accepting one offer (and implicitly declining the rest), or renewing/rebinding.</summary>
    Request = 3,

    /// <summary>Client to server: the offered/assigned address is already in use.</summary>
    Decline = 4,

    /// <summary>Server to client: the request is committed - here is your configuration.</summary>
    Ack = 5,

    /// <summary>Server to client: the request cannot be satisfied - start over.</summary>
    Nak = 6,

    /// <summary>Client to server: relinquishing the lease.</summary>
    Release = 7,
}

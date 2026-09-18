namespace NetSim.Core.Dhcp;

/// <summary>
/// The DHCP client state machine (brief section 9). Not every obscure RFC 2131 transition is
/// modelled, but the main lifecycle is coherent:
/// <code>
/// Init --DISCOVER--> Selecting --OFFER/REQUEST--> Requesting --ACK--> Bound
///                                                              --NAK--> Init
/// Bound --T1--> Renewing --ACK--> Bound
///               Renewing --T2--> Rebinding --ACK--> Bound / --lease end--> Expired --> Init
/// </code>
/// </summary>
public enum DhcpClientState
{
    /// <summary>No lease and not currently trying to obtain one.</summary>
    Init,

    /// <summary>A DISCOVER has been sent; the client is collecting OFFERs.</summary>
    Selecting,

    /// <summary>An OFFER has been selected and a REQUEST sent; awaiting ACK/NAK.</summary>
    Requesting,

    /// <summary>A lease is held and its configuration is applied to the interface.</summary>
    Bound,

    /// <summary>Past T1: unicasting a REQUEST to the leasing server to extend the lease.</summary>
    Renewing,

    /// <summary>Past T2: broadcasting a REQUEST to any server to extend the lease.</summary>
    Rebinding,

    /// <summary>
    /// The client knows its own address/subnet from a previous lease and is verifying it with a
    /// REQUEST rather than a fresh DISCOVER (RFC 2131 INIT-REBOOT). Modelled as a state a caller can
    /// set; the acquisition path itself always starts from <see cref="Init"/> in this phase.
    /// </summary>
    InitReboot,

    /// <summary>The lease expired without a successful renewal; the configuration has been withdrawn.</summary>
    Expired,
}

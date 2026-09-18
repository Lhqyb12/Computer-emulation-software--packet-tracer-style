namespace NetSim.Core.Dhcp;

/// <summary>The lifecycle state of a <see cref="DhcpLease"/> on the server side (brief section 12).</summary>
public enum DhcpLeaseState
{
    /// <summary>Reserved for a client that has been sent an OFFER but has not yet REQUESTed it.</summary>
    Offered,

    /// <summary>Committed to a client by an ACK and currently valid.</summary>
    Active,

    /// <summary>The lease time elapsed without renewal - the address is available for reuse (brief section 14).</summary>
    Expired,

    /// <summary>The client sent a RELEASE - the address is available for reuse (brief section 18).</summary>
    Released,

    /// <summary>The client sent a DECLINE - the address is held out of the pool as unusable (brief section 19).</summary>
    Declined,
}

namespace NetSim.Core.Transport;

/// <summary>
/// The IANA-style range a <see cref="Port"/> number falls into. Purely descriptive - nothing in
/// this phase hard-codes an application service on a well-known port (brief section 5: "Do not
/// hard-code application services such as DNS or DHCP in this phase"); this only classifies the
/// number so a future phase (DNS on 53, DHCP on 67/68, ...) has somewhere to register against.
/// </summary>
public enum PortCategory
{
    /// <summary>0-1023 - traditionally reserved for standard, well-known services.</summary>
    WellKnown,

    /// <summary>1024-49151 - registered with IANA for a specific application, but not privileged.</summary>
    Registered,

    /// <summary>49152-65535 - ephemeral/private ports, typically chosen by a client for its source port.</summary>
    DynamicOrPrivate,
}

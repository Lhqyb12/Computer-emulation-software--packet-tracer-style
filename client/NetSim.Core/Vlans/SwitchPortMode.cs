namespace NetSim.Core.Vlans;

/// <summary>
/// How a switch port participates in VLANs. Phase 26 implements only the two fundamental modes;
/// dynamic negotiation (DTP), private VLANs, QinQ tunnelling and so on are deliberately out of
/// scope - a port's mode is always set by explicit configuration.
/// </summary>
public enum SwitchPortMode
{
    /// <summary>
    /// Carries exactly one VLAN (the port's access VLAN). Traffic to and from an end host - a PC,
    /// server or printer - is untagged. This is the default mode for every port.
    /// </summary>
    Access,

    /// <summary>
    /// Carries several VLANs between switches. Frames for a non-native VLAN are 802.1Q-tagged;
    /// frames for the native VLAN are untagged. The set of VLANs a trunk carries is its allowed
    /// VLAN list.
    /// </summary>
    Trunk,
}

using NetSim.Core.Common;
using NetSim.Core.Networking;

namespace NetSim.Application.Canvas;

/// <summary>
/// One device interface as the Connect tool sees it on the canvas: which device/interface it is,
/// its computed world-space <see cref="Anchor"/>, and enough state to drive highlighting and the
/// hover tooltip (already cabled? administratively enabled? what speed/link state?). Transient -
/// rebuilt from the live topology + canvas item positions whenever they change, never stored.
/// </summary>
public sealed record InterfaceEndpoint(
    EntityId DeviceId,
    int InterfaceIndex,
    string InterfaceName,
    string ShortName,
    InterfaceType InterfaceType,
    InterfaceCapability Capabilities,
    InterfaceSpeed Speed,
    InterfaceAdministrativeState AdministrativeState,
    InterfaceOperationalState OperationalState,
    CanvasPoint Anchor,
    bool IsConnected)
{
    public bool IsEnabled => AdministrativeState == InterfaceAdministrativeState.Enabled;

    /// <summary>Can this interface accept a new connection right now?</summary>
    public bool IsAvailable => IsEnabled && !IsConnected;

    public bool SameInterfaceAs(InterfaceEndpoint other) =>
        other is not null && DeviceId == other.DeviceId && InterfaceIndex == other.InterfaceIndex;

    /// <summary>Concise multi-line text for a canvas hover tooltip.</summary>
    public string ToTooltip()
    {
        var status = !IsEnabled ? "Disabled"
            : OperationalState == InterfaceOperationalState.Up ? "Up"
            : "Down";
        var connection = IsConnected ? "Connected" : "Not connected";
        return $"{InterfaceName}\nType: {InterfaceTypeInfo.DisplayName(InterfaceType)}\nSpeed: {Speed.ToDisplayString()}\nStatus: {status}\nConnection: {connection}";
    }
}

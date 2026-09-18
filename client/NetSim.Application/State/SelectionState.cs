using System;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.State;

public sealed class SelectionState : ISelectionState
{
    public NetworkDevice? SelectedDevice { get; private set; }

    public NetworkInterface? SelectedInterface { get; private set; }

    public Connection? SelectedConnection { get; private set; }

    public event EventHandler? SelectionChanged;

    public void SelectDevice(NetworkDevice? device) => SetSelection(device, null, null);

    public void SelectInterface(NetworkInterface? networkInterface) => SetSelection(null, networkInterface, null);

    public void SelectConnection(Connection? connection) => SetSelection(null, null, connection);

    public void ClearSelection() => SetSelection(null, null, null);

    private void SetSelection(NetworkDevice? device, NetworkInterface? networkInterface, Connection? connection)
    {
        if (ReferenceEquals(SelectedDevice, device)
            && ReferenceEquals(SelectedInterface, networkInterface)
            && ReferenceEquals(SelectedConnection, connection))
        {
            return;
        }

        SelectedDevice = device;
        SelectedInterface = networkInterface;
        SelectedConnection = connection;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}

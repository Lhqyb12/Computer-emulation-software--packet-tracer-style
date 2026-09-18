using System;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.State;

/// <summary>
/// Minimal seam for the future Network Canvas: tracks which single item (device,
/// interface, or connection) is currently selected. Selecting one kind of item clears
/// the others, since only one thing can be "selected" on the canvas at a time. No canvas
/// behavior is implemented yet ג€” this only prepares the state to be observed later.
/// </summary>
public interface ISelectionState
{
    NetworkDevice? SelectedDevice { get; }

    NetworkInterface? SelectedInterface { get; }

    Connection? SelectedConnection { get; }

    event EventHandler? SelectionChanged;

    void SelectDevice(NetworkDevice? device);

    void SelectInterface(NetworkInterface? networkInterface);

    void SelectConnection(Connection? connection);

    void ClearSelection();
}

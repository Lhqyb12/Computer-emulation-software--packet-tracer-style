using NetSim.Application.Canvas;   // brings in CanvasPoint - a place on the canvas
using NetSim.Application.Common;   // brings in OperationResult - a "success / failure + why" answer
using NetSim.Core.Devices;         // brings in NetworkDevice (a device) and DeviceType (its kind)

namespace NetSim.Application.Services;   // the "address" of this file: the Services part of the Application layer

// The rules of devices: adding, moving, renaming and removing a device in the open project.
// It is the ONE door for these changes, and it keeps two things in step every time:
//   1. the device in the network (Core's Network - what the device IS and what it is plugged into);
//   2. its item on the canvas (ICanvasItemsState - WHERE it is drawn).
// So a device can never exist on the canvas without being in the network, or the other way round.
// Expected problems (no project open, empty or taken name, unknown device) come back as a failure answer
// (OperationResult) instead of an exception, so the screen can show a message instead of crashing.
// "interface" = a list of WHAT the class can do, without HOW (DeviceService does the work)
public interface IDeviceService
{
    // Purpose:       Adds a new device to the open network and puts it on the canvas.
    // Precondition:  receives deviceType - which kind of device to add (PC, Switch...);
    //                center - the world point the user clicked (the device's middle goes there);
    //                name - the device's name, or null (the default "= null") to pick a free one like "Router0".
    // Postcondition: returns an OperationResult<NetworkDevice>: success + the new device (it is in the network with its
    //                starting ports, its item is on the canvas, the project is marked "unsaved"), or failure + why
    //                (InvalidState: no project; ValidationError: empty name; Conflict: the name is taken) - then nothing changed.
    OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, CanvasPoint center, string? name = null);

    // Purpose:       Moves a device to a new place on the canvas.
    // Precondition:  receives device - the device to move (it must be in the open network);
    //                newPosition - its new top-left corner, as a world point.
    // Postcondition: returns an OperationResult: success (the item is at the new place, the project is marked
    //                "unsaved"), or failure + why (InvalidState / NotFound) - then nothing changed.
    OperationResult MoveDevice(NetworkDevice device, CanvasPoint newPosition);

    // Purpose:       Gives a device a new name (in the network AND under its icon).
    // Precondition:  receives device - the device (it must be in the open network); newName - the new name
    //                (not empty, and not used by another device).
    // Postcondition: returns an OperationResult: success (the device and its item have the new, trimmed name, the
    //                project is marked "unsaved"), or failure + why (InvalidState / NotFound / ValidationError / Conflict).
    OperationResult RenameDevice(NetworkDevice device, string newName);

    // Purpose:       Removes a device from the open network and from the canvas, with all its cables.
    // Precondition:  receives device - the device to remove (it must be in the open network).
    // Postcondition: returns an OperationResult: success (the device, its cables and its item are gone, the project
    //                is marked "unsaved"), or failure + why (InvalidState / NotFound) - then nothing changed.
    OperationResult RemoveDevice(NetworkDevice device);
}

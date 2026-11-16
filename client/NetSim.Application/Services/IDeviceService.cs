using NetSim.Application.Canvas;   // CanvasPoint - a place on the canvas
using NetSim.Application.Common;   // OperationResult - success / failure answers
using NetSim.Core.Devices;         // NetworkDevice, DeviceType

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of devices: adding, moving, renaming and removing a device in the open project.
// It is the ONE door for these changes, and it keeps two things in step every time:
//   1. the device in the network (Core's Network - what the device IS and what it is plugged into);
//   2. its item on the canvas (ICanvasItemsState - WHERE it is drawn).
// So a device can never exist on the canvas without being in the network, or the other way round.
// Expected problems (no project open, empty or taken name, unknown device) come back as a
// failure answer instead of an exception, so the screen can show a message.
// An interface = a list of what the class can do, without how
public interface IDeviceService
{
    // Purpose:       Adds a new device to the open network and puts it on the canvas.
    // Precondition:  a project is open. name is null (= pick a free name like "Router0") or a
    //                non-empty name no other device has. center is the world point the user clicked.
    // Postcondition: on success - the device (with its starting ports) is in the network, its item is
    //                on the canvas centered on the given point, and the project is marked "unsaved".
    //                On failure - nothing changed (InvalidState: no project; ValidationError: empty
    //                name; Conflict: the name is taken).
    OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, CanvasPoint center, string? name = null);

    // Purpose:       Moves a device to a new place on the canvas.
    // Precondition:  a project is open and the device is in its network. newPosition is the new
    //                top-left corner, as a world point.
    // Postcondition: on success - the device's item is at the new place and the project is marked
    //                "unsaved". On failure (InvalidState / NotFound) - nothing changed.
    OperationResult MoveDevice(NetworkDevice device, CanvasPoint newPosition);

    // Purpose:       Gives a device a new name (in the network and under its icon).
    // Precondition:  a project is open, the device is in its network, and newName is not empty and
    //                not used by another device.
    // Postcondition: on success - the device and its item have the new name (trimmed) and the project
    //                is marked "unsaved". On failure (InvalidState / NotFound / ValidationError /
    //                Conflict) - nothing changed.
    OperationResult RenameDevice(NetworkDevice device, string newName);

    // Purpose:       Removes a device from the open network and from the canvas, with all its cables.
    // Precondition:  a project is open and the device is in its network.
    // Postcondition: on success - the device, its cables and its item are gone and the project is
    //                marked "unsaved". On failure (InvalidState / NotFound) - nothing changed.
    OperationResult RemoveDevice(NetworkDevice device);
}

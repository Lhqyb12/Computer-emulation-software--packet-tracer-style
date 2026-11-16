using System;                        // ArgumentNullException, StringComparison
using System.Linq;                   // Any - "is there at least one..."
using NetSim.Application.Canvas;     // ICanvasItemsState, CanvasItem, CanvasPoint
using NetSim.Application.Common;     // OperationResult, OperationErrorType
using NetSim.Application.State;      // IApplicationState - the open network, and "unsaved changes"
using NetSim.Core.Devices;           // NetworkDevice, DeviceType, NetworkDeviceFactory, DeviceTypeRegistry
using NetSim.Core.Topology;          // Network

namespace NetSim.Application.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of IDeviceService.
// Changes from the reference project:
//   - Adding a device also puts it on the canvas in the same call (there the screen had to do the
//     two steps by itself, and could forget one).
//   - Moving goes through here too, so a move also marks the project "unsaved".
//   - An empty or taken name comes back as a failure answer instead of an exception.
//   - The default name takes the lowest FREE number ("Router0", "Router1"...), so deleting a device
//     and adding a new one can't produce two devices with the same name.
//   - The address / gateway / port methods were left out - they come in steps 10-11.
// "sealed" = no other class can inherit from it
public sealed class DeviceService : IDeviceService
{
    private readonly IApplicationState _applicationState;   // the open network, and "unsaved changes"
    private readonly ICanvasItemsState _canvasItems;        // where every device is drawn

    // Purpose:       Builds the service.
    // Precondition:  applicationState and canvasItems are not null (the DI container passes them in).
    // Postcondition: the service is ready to use.
    public DeviceService(IApplicationState applicationState, ICanvasItemsState canvasItems)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(canvasItems);

        _applicationState = applicationState;   // keep the open-network state
        _canvasItems = canvasItems;             // keep the canvas positions
    }

    // Purpose:       Adds a new device to the open network and puts it on the canvas.
    // Precondition:  a project is open; name is null or a non-empty, free name.
    // Postcondition: success - device in the network + item on the canvas + project "unsaved";
    //                failure - nothing changed.
    public OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, CanvasPoint center, string? name = null)
    {
        // No open network = no project is open - nothing to add to
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<NetworkDevice>.Failure(OperationErrorType.InvalidState, "No project is open. Open or create a project before adding devices.");
        }

        // No name given - pick a free one like "Router0". "??" = use the right side only when the left is null
        var resolvedName = name ?? GenerateDefaultName(network, deviceType);

        // Check the name (empty? taken?). null = it is fine. "excludedDevice: null" = every device counts
        var nameError = ValidateName(network, resolvedName, excludedDevice: null);

        // A problem with the name - return it, nothing was changed
        if (nameError is not null)
        {
            return OperationResult<NetworkDevice>.Failure(nameError.Value.ErrorType, nameError.Value.ErrorMessage);
        }

        // Build the device with its starting ports (e.g. 8 ports for a switch)
        var device = NetworkDeviceFactory.Create(deviceType, resolvedName.Trim());

        // Put it in the network
        network.AddDevice(device);

        // The user clicked where the MIDDLE of the device should be; the item keeps its TOP-LEFT
        // corner, so move back by half the width and half the height
        var size = CanvasItem.DefaultDeviceSize;                                                // the size of a device on the canvas
        var topLeft = new CanvasPoint(center.X - size.Width / 2, center.Y - size.Height / 2);   // the top-left corner

        // Put its item on the canvas, with the SAME id as the device - that id joins the two
        _canvasItems.AddItem(new CanvasItem(device.Id, device.Name, topLeft, size, device.DeviceType));

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Success, with the new device
        return OperationResult<NetworkDevice>.Success(device);
    }

    // Purpose:       Moves a device to a new place on the canvas.
    // Precondition:  a project is open and the device is in its network.
    // Postcondition: success - the item is at the new place + project "unsaved"; failure - nothing changed.
    public OperationResult MoveDevice(NetworkDevice device, CanvasPoint newPosition)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(device);

        // Is there an open network, and is the device in it? null = yes
        var failure = CheckDeviceInOpenNetwork(device, out _);

        // No - return the problem, nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Move its item. false = the device has no item on the canvas (should not happen, but say so instead of hiding it)
        if (!_canvasItems.MoveItem(device.Id, newPosition))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' is not on the canvas.");
        }

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose:       Gives a device a new name (in the network and under its icon).
    // Precondition:  a project is open, the device is in its network, newName is not empty and is free.
    // Postcondition: success - new name on the device and its item + project "unsaved"; failure - nothing changed.
    public OperationResult RenameDevice(NetworkDevice device, string newName)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(device);

        // Is there an open network, and is the device in it? "out var network" = also hand back the network
        var failure = CheckDeviceInOpenNetwork(device, out var network);

        // No - return the problem, nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Check the new name. "excludedDevice: device" = the device itself may keep its own name
        var nameError = ValidateName(network!, newName, excludedDevice: device);

        // A problem with the name - return it, nothing was changed
        if (nameError is not null)
        {
            return OperationResult.Failure(nameError.Value.ErrorType, nameError.Value.ErrorMessage);
        }

        // The name without spaces at both ends
        var trimmed = newName.Trim();

        device.Rename(trimmed);                       // the name in the network
        _canvasItems.RenameItem(device.Id, trimmed);  // the name under the icon

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Success, nothing to return
        return OperationResult.Success();
    }

    // Purpose:       Removes a device from the open network and from the canvas, with all its cables.
    // Precondition:  a project is open and the device is in its network.
    // Postcondition: success - device, cables and item gone + project "unsaved"; failure - nothing changed.
    public OperationResult RemoveDevice(NetworkDevice device)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(device);

        // Is there an open network, and is the device in it?
        var failure = CheckDeviceInOpenNetwork(device, out var network);

        // No - return the problem, nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Remove it from the network. Network.RemoveDevice unplugs and removes all its cables first
        network!.RemoveDevice(device);

        // Take its item off the canvas
        _canvasItems.RemoveItem(device.Id);

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Success, nothing to return
        return OperationResult.Success();
    }

    // ----- Inside helpers -----

    // Purpose:       Checks that a project is open and that a device belongs to its network.
    // Precondition:  device is not null.
    // Postcondition: returns null and hands back the network when both are true; otherwise returns the
    //                failure answer (InvalidState / NotFound). Nothing is changed.
    private OperationResult? CheckDeviceInOpenNetwork(NetworkDevice device, out Network? network)
    {
        // The open network (null = no project is open). "out" = the caller gets it too
        network = _applicationState.CurrentNetwork;

        // No project is open
        if (network is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is open.");
        }

        // The device is not in this network (e.g. it was already removed)
        if (!network.ContainsDevice(device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' was not found in the open network.");
        }

        // All good - no failure
        return null;
    }

    // Purpose:       Checks a device name: not empty, and not used by another device in the network.
    // Precondition:  network is not null. excludedDevice = a device that may keep this name (itself,
    //                when renaming), or null.
    // Postcondition: returns null when the name is fine, or a pair (error type, message). Nothing is changed.
    //                "(OperationErrorType ErrorType, string ErrorMessage)?" = two values travelling together, or null
    private static (OperationErrorType ErrorType, string ErrorMessage)? ValidateName(Network network, string? name, NetworkDevice? excludedDevice)
    {
        // Empty, or only spaces
        if (string.IsNullOrWhiteSpace(name))
        {
            return (OperationErrorType.ValidationError, "Device name cannot be empty.");
        }

        // The name without spaces at both ends
        var trimmed = name.Trim();

        // Is there ANOTHER device with this name (any upper / lower case)?
        // Not allowed, because later the terminal (step 5) and the AI find devices by their name
        var taken = network.Devices.Any(d =>
            !ReferenceEquals(d, excludedDevice) &&                                // not the device being renamed
            string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase));  // and the same name

        // Taken - say so
        if (taken)
        {
            return (OperationErrorType.Conflict, $"A device named '{trimmed}' already exists.");
        }

        // No problem
        return null;
    }

    // Purpose:       Picks a free default name for a new device: the kind's name + the lowest free number.
    // Precondition:  network is not null.
    // Postcondition: returns a name no device in the network has, e.g. "Router0", "Switch2", "PC1".
    //                Nothing is changed.
    private static string GenerateDefaultName(Network network, DeviceType deviceType)
    {
        // The kind's display name without spaces (e.g. "Router", "PC") - the start of the name
        var prefix = DeviceTypeRegistry.Get(deviceType).DisplayName.Replace(" ", string.Empty);

        // Try 0, 1, 2... and stop at the first number no device uses
        for (var number = 0; ; number++)
        {
            // The name to try, e.g. "Router0". "$" = put the values inside the text
            var candidate = $"{prefix}{number}";

            // Does any device already have this name (any upper / lower case)?
            var taken = network.Devices.Any(d => string.Equals(d.Name, candidate, StringComparison.OrdinalIgnoreCase));

            // Free - this is the name
            if (!taken)
            {
                return candidate;
            }
        }
    }
}

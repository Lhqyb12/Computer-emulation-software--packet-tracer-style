using System;                        // brings in ArgumentNullException and StringComparison (how to compare text)
using System.Linq;                   // brings in Any - "is there at least one item in the list that passes this test?"
using NetSim.Application.Canvas;     // brings in ICanvasItemsState, CanvasItem, CanvasPoint - the canvas positions
using NetSim.Application.Common;     // brings in OperationResult and OperationErrorType - success / failure answers
using NetSim.Application.State;      // brings in IApplicationState - the open network, and the "unsaved changes" flag
using NetSim.Core.Devices;           // brings in NetworkDevice, DeviceType, NetworkDeviceFactory, DeviceTypeRegistry
using NetSim.Core.Topology;          // brings in Network - all the devices and cables of the project

namespace NetSim.Application.Services;   // the "address" of this file: the Services part of the Application layer

// The class that does the work of IDeviceService.
// Changes from the reference project:
//   - Adding a device also puts it on the canvas in the same call (there the screen had to do the two
//     steps by itself, and could forget one).
//   - Moving goes through here too, so a move also marks the project "unsaved".
//   - An empty or taken name comes back as a failure answer instead of an exception.
//   - The default name takes the lowest FREE number ("Router0", "Router1"...), so deleting a device
//     and adding a new one can't produce two devices with the same name.
//   - The address / gateway / port methods were left out - they come in steps 10-11.
// "sealed" = no other class can inherit from it; ": IDeviceService" = it does everything the interface lists
public sealed class DeviceService : IDeviceService
{
    // "private readonly" = only this class sees them, and they are set once (in the constructor) and never replaced
    private readonly IApplicationState _applicationState;   // the open network, and the "unsaved changes" flag
    private readonly ICanvasItemsState _canvasItems;        // where every device is drawn on the canvas

    // Purpose:       Builds the service.
    // Precondition:  receives applicationState - the app's "what is open" state; canvasItems - the canvas positions
    //                (both passed in by the DI container; neither may be null).
    // Postcondition: returns nothing (a constructor) - the service is ready to use.
    public DeviceService(IApplicationState applicationState, ICanvasItemsState canvasItems)
    {
        // If either one is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(canvasItems);

        _applicationState = applicationState;   // keep the open-network state, to use in every method
        _canvasItems = canvasItems;             // keep the canvas positions, to use in every method
    }

    // Purpose:       Adds a new device to the open network and puts it on the canvas.
    // Precondition:  receives deviceType - the kind; center - the world point for the device's middle;
    //                name - a name, or null to pick a free one.
    // Postcondition: returns success + the new device (network + canvas + "unsaved"), or failure + why (nothing changed).
    public OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, CanvasPoint center, string? name = null)
    {
        // "is not { } network" = if CurrentNetwork is null -> go inside the "if"; if it is NOT null, it is kept in the
        // new variable "network" for the rest of the method. No network = no project is open = nothing to add to
        if (_applicationState.CurrentNetwork is not { } network)
        {
            // Hand back a failure: the kind of problem (InvalidState = "can't do this right now") + a message for the screen
            return OperationResult<NetworkDevice>.Failure(OperationErrorType.InvalidState, "No project is open. Open or create a project before adding devices.");
        }

        // "a ?? b" = a, but if a is null then b. No name was given -> pick a free one like "Router0" (GenerateDefaultName, below)
        var resolvedName = name ?? GenerateDefaultName(network, deviceType);

        // Check the name: is it empty? is it taken? ValidateName (below) returns null when it is fine, or a problem.
        // "excludedDevice: null" = a named argument: no device is excluded - EVERY device's name counts as taken
        var nameError = ValidateName(network, resolvedName, excludedDevice: null);

        // "is not null" = there IS a problem with the name - hand it back as a failure; nothing was changed
        if (nameError is not null)
        {
            // nameError is a "nullable" pair; ".Value" takes the pair out of it: its ErrorType and its ErrorMessage
            return OperationResult<NetworkDevice>.Failure(nameError.Value.ErrorType, nameError.Value.ErrorMessage);
        }

        // Build the device with all its starting ports (e.g. a switch gets 8 ports) - NetworkDeviceFactory from step 1.
        // Trim() = the name without spaces at its start and end
        var device = NetworkDeviceFactory.Create(deviceType, resolvedName.Trim());

        // Put the device into the network (Network.AddDevice, step 1)
        network.AddDevice(device);

        // The user chose where the MIDDLE of the device goes, but the canvas item keeps its TOP-LEFT corner,
        // so we move back by half the width and half the height
        var size = CanvasItem.DefaultDeviceSize;                                                // the size of every device on the canvas (96 x 72)
        var topLeft = new CanvasPoint(center.X - size.Width / 2, center.Y - size.Height / 2);   // middle - half size = top-left corner

        // Put the device's item on the canvas, with the SAME id as the device - that id is what joins the two.
        // device.Name = its label; device.DeviceType = for the icon
        _canvasItems.AddItem(new CanvasItem(device.Id, device.Name, topLeft, size, device.DeviceType));

        // The project now has a change that was not saved (the screen can ask "save before leaving?")
        _applicationState.MarkCurrentProjectDirty();

        // Hand back a success, with the new device inside it
        return OperationResult<NetworkDevice>.Success(device);
    }

    // Purpose:       Moves a device to a new place on the canvas.
    // Precondition:  receives device - the device (in the open network); newPosition - its new top-left corner (world).
    // Postcondition: returns success (item moved + "unsaved"), or failure + why (nothing changed).
    public OperationResult MoveDevice(NetworkDevice device, CanvasPoint newPosition)
    {
        // If no device was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(device);

        // Is a project open, and is the device in its network? CheckDeviceInOpenNetwork (below) returns null when yes,
        // or the failure answer when no. "out _" = it also hands back the network, but here we don't need it
        var failure = CheckDeviceInOpenNetwork(device, out _);

        // There is a problem - hand it back; nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Move the device's item. "!" = NOT: MoveItem returns false when the device has no item on the canvas.
        // That should never happen, but if it does we say so instead of hiding it
        if (!_canvasItems.MoveItem(device.Id, newPosition))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' is not on the canvas.");
        }

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Hand back a success (nothing else to return)
        return OperationResult.Success();
    }

    // Purpose:       Gives a device a new name (in the network and under its icon).
    // Precondition:  receives device - the device (in the open network); newName - the new name (not empty, free).
    // Postcondition: returns success (new name on both + "unsaved"), or failure + why (nothing changed).
    public OperationResult RenameDevice(NetworkDevice device, string newName)
    {
        // If no device was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(device);

        // Is a project open, and is the device in its network? "out var network" = also hand the network back to us
        var failure = CheckDeviceInOpenNetwork(device, out var network);

        // There is a problem - hand it back; nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Check the new name. "excludedDevice: device" = the device itself may keep its own name (renaming "PC0" to
        // "pc0" must not fail just because "PC0" exists). "network!" = "!" tells the compiler "I know this is not null"
        // - true here, because CheckDeviceInOpenNetwork just passed
        var nameError = ValidateName(network!, newName, excludedDevice: device);

        // There is a problem with the name - hand it back; nothing was changed
        if (nameError is not null)
        {
            return OperationResult.Failure(nameError.Value.ErrorType, nameError.Value.ErrorMessage);
        }

        // The new name without spaces at its start and end
        var trimmed = newName.Trim();

        device.Rename(trimmed);                       // the name of the device in the network (NetworkDevice.Rename)
        _canvasItems.RenameItem(device.Id, trimmed);  // the text under its icon on the canvas - so the two always match

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Hand back a success
        return OperationResult.Success();
    }

    // Purpose:       Removes a device from the open network and from the canvas, with all its cables.
    // Precondition:  receives device - the device to remove (in the open network).
    // Postcondition: returns success (device, cables and item gone + "unsaved"), or failure + why (nothing changed).
    public OperationResult RemoveDevice(NetworkDevice device)
    {
        // If no device was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(device);

        // Is a project open, and is the device in its network? Also hand the network back to us
        var failure = CheckDeviceInOpenNetwork(device, out var network);

        // There is a problem - hand it back; nothing was changed
        if (failure is not null)
        {
            return failure;
        }

        // Remove the device from the network. Network.RemoveDevice (step 1) first unplugs and removes every cable on
        // it, so no cable is left pointing at a device that is gone
        network!.RemoveDevice(device);

        // Take its item off the canvas too
        _canvasItems.RemoveItem(device.Id);

        // The project now has a change that was not saved
        _applicationState.MarkCurrentProjectDirty();

        // Hand back a success
        return OperationResult.Success();
    }

    // ----- Inside helpers -----

    // Purpose:       Checks that a project is open and that a device belongs to its network.
    // Precondition:  receives device - the device to check (not null); network - an "out" box the function fills.
    // Postcondition: returns null when both are true (and network holds the open network), otherwise the failure
    //                answer: InvalidState (no project open) or NotFound (the device is not in the network).
    // "OperationResult?" = a failure answer, or null for "no failure"
    private OperationResult? CheckDeviceInOpenNetwork(NetworkDevice device, out Network? network)
    {
        // Fill the "out" box with the open network (null when no project is open) - the caller gets it too
        network = _applicationState.CurrentNetwork;

        // No project is open
        if (network is null)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No project is open.");
        }

        // The device is not in this network (e.g. it was already removed). ContainsDevice = Network's own check (step 1)
        if (!network.ContainsDevice(device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' was not found in the open network.");
        }

        // Both checks passed - no failure
        return null;
    }

    // Purpose:       Checks a device name: not empty, and not used by another device in the network.
    // Precondition:  receives network - the open network (not null); name - the name to check (may be null);
    //                excludedDevice - a device that may keep this name (itself, when renaming), or null.
    // Postcondition: returns null when the name is fine, or a pair (error type, message) describing the problem.
    // "(OperationErrorType ErrorType, string ErrorMessage)?" = two values travelling together (a "tuple"), or null.
    // "static" = it doesn't use this object's fields, only what it receives
    private static (OperationErrorType ErrorType, string ErrorMessage)? ValidateName(Network network, string? name, NetworkDevice? excludedDevice)
    {
        // null, "" or only spaces - not a name
        if (string.IsNullOrWhiteSpace(name))
        {
            // "( a, b )" = build the pair: ValidationError = "what the user typed breaks a rule" + the message
            return (OperationErrorType.ValidationError, "Device name cannot be empty.");
        }

        // The name without spaces at its start and end
        var trimmed = name.Trim();

        // Is there ANOTHER device with this name? Any(...) goes over all the devices and answers true if the test is
        // true for at least one. The test ("d => ...", d = one device):
        var taken = network.Devices.Any(d =>
            !ReferenceEquals(d, excludedDevice) &&                                // it is not the device being renamed ("ReferenceEquals" = the very same object)
            string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase));  // and its name is the same, ignoring capital / small letters ("PC0" = "pc0")

        // The name is taken. It is not allowed, because later the terminal (part 5) and the AI find devices by name
        if (taken)
        {
            // Conflict = "it clashes with something that already exists"
            return (OperationErrorType.Conflict, $"A device named '{trimmed}' already exists.");
        }

        // No problem with the name
        return null;
    }

    // Purpose:       Picks a free default name for a new device: the kind's name + the lowest free number.
    // Precondition:  receives network - the open network (not null); deviceType - the kind of the new device.
    // Postcondition: returns a name no device in the network has yet, e.g. "Router0", "Switch2", "PC1".
    private static string GenerateDefaultName(Network network, DeviceType deviceType)
    {
        // The kind's screen name (from DeviceTypeRegistry, step 1), e.g. "Router" or "PC".
        // Replace(" ", string.Empty) = remove any spaces, so a name never has a space in it
        var prefix = DeviceTypeRegistry.Get(deviceType).DisplayName.Replace(" ", string.Empty);

        // Try the numbers 0, 1, 2... one by one. "for (start; ; step)" with NO condition in the middle = it does not
        // stop by itself - it stops only at the "return" below, when a free name is found
        for (var number = 0; ; number++)
        {
            // The name to try, e.g. "Router0". "$" puts the values inside {...} into the text
            var candidate = $"{prefix}{number}";

            // Does any device already have this name (ignoring capital / small letters)?
            var taken = network.Devices.Any(d => string.Equals(d.Name, candidate, StringComparison.OrdinalIgnoreCase));

            // Free - this is the name; stop and hand it back
            if (!taken)
            {
                return candidate;
            }
        }
        // Why "the lowest FREE number" and not "how many devices there are": with Router0 and Router1, deleting Router0
        // and counting would give "Router1" again - a duplicate. Searching for the lowest free number gives "Router0"
    }
}

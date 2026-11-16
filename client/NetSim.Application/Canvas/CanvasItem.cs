using System;                  // ArgumentException
using NetSim.Core.Common;      // EntityId - the device's unique id
using NetSim.Core.Devices;     // DeviceType - which icon to draw

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One device as it sits on the canvas: WHERE it is, how big it is, and what text is under it.
// The device itself (its ports, its cables) lives in Core's Network; this class only holds the
// drawing details. They are joined by the id: CanvasItem.Id is exactly the device's own Id.
// Change from the reference project: there an item could also be "a plain box with no device";
// here every item IS a device, so DeviceType is always filled.
// "sealed" = no other class can inherit from it
public sealed class CanvasItem
{
    // The size every device takes on the canvas (96 wide, 72 high) - room for the icon and its name.
    // One constant for all devices, so the canvas looks tidy
    public static readonly CanvasSize DefaultDeviceSize = new(96, 72);

    // Purpose:       Builds the canvas details of one device.
    // Precondition:  id is the id of a device in the open network; label is not empty;
    //                size has a width and a height above 0.
    // Postcondition: a new item exists at the given position, with the given label, size and kind.
    //                Throws ArgumentException when the label is empty or the size is not above 0.
    public CanvasItem(EntityId id, string label, CanvasPoint position, CanvasSize size, DeviceType deviceType)
    {
        // An empty label would show a nameless device - stop with a clear message
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        // A size of 0 or less can't be clicked or drawn - stop with a clear message
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentException("A canvas item must have a width and a height above 0.", nameof(size));
        }

        Id = id;                    // the device's id
        Label = label;              // the text under the icon
        Position = position;        // the top-left corner on the canvas
        Size = size;                // how much room it takes
        DeviceType = deviceType;    // which kind of device, for the icon
    }

    // The device's id (the same id the device has in the Network). Never changes
    public EntityId Id { get; }

    // The text shown under the icon (the device name). "private set" = only this class changes it, through Rename()
    public string Label { get; private set; }

    // The top-left corner of the item on the canvas (world point). Changes only through MoveTo()
    public CanvasPoint Position { get; private set; }

    // How much room it takes. Never changes
    public CanvasSize Size { get; }

    // Which kind of device it is - the UI picks the icon by it. Never changes
    public DeviceType DeviceType { get; }

    // The middle of the item. "=>" = worked out again every time it is read, so it is always
    // right after a move. Step 6 (cables) will draw each cable from one center to the other
    public CanvasPoint Center => new(Position.X + Size.Width / 2, Position.Y + Size.Height / 2);

    // Purpose:       Puts the item at a new place on the canvas.
    // Precondition:  position is a world point (not a screen point).
    // Postcondition: Position equals the given point; nothing else changed.
    public void MoveTo(CanvasPoint position) => Position = position;   // keep the new top-left corner

    // Purpose:       Changes the text under the icon (used when the device is renamed).
    // Precondition:  label is not empty.
    // Postcondition: Label equals the given text. Throws ArgumentException when it is empty.
    public void Rename(string label)
    {
        // An empty label is not allowed - stop with a clear message
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label;   // keep the new text
    }

    // Purpose:       Answers whether a world point falls inside this item (used to find what was clicked).
    // Precondition:  worldPoint is a world point.
    // Postcondition: returns true when the point is inside the item's box (edges count), otherwise false.
    //                Nothing is changed.
    public bool Contains(CanvasPoint worldPoint) =>
        worldPoint.X >= Position.X && worldPoint.X <= Position.X + Size.Width &&    // between the left and right edges
        worldPoint.Y >= Position.Y && worldPoint.Y <= Position.Y + Size.Height;     // and between the top and bottom edges
}

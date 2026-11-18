using System;                  // brings in ArgumentException - the error we throw when a bad value (empty label, size 0) is passed in
using NetSim.Core.Common;      // brings in EntityId - the unique id of the device this item draws
using NetSim.Core.Devices;     // brings in DeviceType - which kind of device it is (the screen picks the icon by it)

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// One device as it sits on the canvas: WHERE it is, how big it is, and what text is under it.
// The device itself (its ports, its cables) lives in Core's Network; this class only holds the drawing details.
// The two are joined by the id: CanvasItem.Id is exactly the device's own Id.
// Change from the reference project: there an item could also be "a plain box with no device";
// here every item IS a device, so DeviceType is always filled.
// "sealed" = no other class can inherit from (extend) this one.
// It is a "class" (not a struct) because an item CHANGES (it moves, it is renamed) and everyone must see the same item
public sealed class CanvasItem
{
    // The size every device takes on the canvas: 96 wide, 72 high - room for the icon and the name under it.
    // One shared value for all devices, so the canvas looks tidy.
    // "public static readonly" = everyone may read it, there is one copy, and it can never be replaced
    public static readonly CanvasSize DefaultDeviceSize = new(96, 72);

    // Purpose:       Builds the canvas details of one device.
    // Precondition:  receives id - the device's id (the same id the device has in the Network);
    //                label - the text under the icon (must not be empty);
    //                position - the item's top-left corner, as a world point;
    //                size - its width and height (both must be above 0);
    //                deviceType - which kind of device it is.
    // Postcondition: returns nothing (a constructor) - a new item exists with these values.
    //                Throws ArgumentException when the label is empty or the size is not above 0.
    public CanvasItem(EntityId id, string label, CanvasPoint position, CanvasSize size, DeviceType deviceType)
    {
        // If the label is null, "" or only spaces, stop right away with a clear error - a device with no name on
        // the canvas would be a bug
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        // "||" = OR: if the width is 0 or less OR the height is 0 or less, the item could not be drawn or clicked
        if (size.Width <= 0 || size.Height <= 0)
        {
            // "throw" = stop and report the error. nameof(size) = the parameter's name as text ("size"),
            // so the message says exactly which value was wrong
            throw new ArgumentException("A canvas item must have a width and a height above 0.", nameof(size));
        }

        Id = id;                    // keep the device's id (the link to the device in the Network)
        Label = label;              // keep the text shown under the icon
        Position = position;        // keep the top-left corner on the canvas
        Size = size;                // keep how much room it takes
        DeviceType = deviceType;    // keep which kind of device it is, for the icon
    }

    // The device's id. Only "get" = it can be read but never changed after the item is built
    public EntityId Id { get; }

    // The text shown under the icon (the device's name).
    // "private set" = everyone can read it, but only this class can change it - through Rename()
    public string Label { get; private set; }

    // The item's top-left corner on the canvas (a world point). Only this class changes it - through MoveTo()
    public CanvasPoint Position { get; private set; }

    // How much room the item takes. Never changes after it is built
    public CanvasSize Size { get; }

    // Which kind of device it is - the screen picks the icon by it. Never changes
    public DeviceType DeviceType { get; }

    // The middle of the item: the top-left corner + half the width, + half the height.
    // "=>" here = a computed property: it is worked out again EVERY time it is read, so after a move it is
    // still right. The cables (step 6) are drawn from one device's Center to the other's
    public CanvasPoint Center => new(Position.X + Size.Width / 2, Position.Y + Size.Height / 2);

    // Purpose:       Puts the item at a new place on the canvas.
    // Precondition:  receives position - the new top-left corner, as a WORLD point (not a screen point).
    // Postcondition: returns nothing - Position now equals the given point; nothing else changed.
    // "=>" = a one-line function body: it just stores the new value
    public void MoveTo(CanvasPoint position) => Position = position;

    // Purpose:       Changes the text under the icon (used when the device is renamed).
    // Precondition:  receives label - the new text (must not be empty).
    // Postcondition: returns nothing - Label now equals the new text. Throws ArgumentException when it is empty.
    public void Rename(string label)
    {
        // If the new label is null, "" or only spaces, stop with a clear error
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        // Keep the new text
        Label = label;
    }

    // Purpose:       Answers whether a world point falls inside this item - used to find what the user clicked.
    // Precondition:  receives worldPoint - the point to check, as a world point.
    // Postcondition: returns true when the point is inside the item's box (its edges count as inside), otherwise false.
    // "&&" = AND: ALL four comparisons must be true
    public bool Contains(CanvasPoint worldPoint) =>
        worldPoint.X >= Position.X && worldPoint.X <= Position.X + Size.Width &&    // X is between the left edge and the right edge (left + width)
        worldPoint.Y >= Position.Y && worldPoint.Y <= Position.Y + Size.Height;     // and Y is between the top edge and the bottom edge (top + height)

    // Purpose:       Answers whether this item's box overlaps a rectangle (step 4: the selection rectangle).
    // Precondition:  receives min - the rectangle's top-left corner; max - its bottom-right corner (world points).
    // Postcondition: returns true when the two boxes share any area, otherwise false.
    // The rule: two boxes overlap when EACH one starts before the OTHER one ends - checked on X and on Y
    public bool Intersects(CanvasPoint min, CanvasPoint max) =>
        Position.X < max.X && Position.X + Size.Width > min.X &&     // the item starts before the rectangle's right edge, and ends after its left edge (they overlap left-to-right)
        Position.Y < max.Y && Position.Y + Size.Height > min.Y;      // and the same top-to-bottom
}

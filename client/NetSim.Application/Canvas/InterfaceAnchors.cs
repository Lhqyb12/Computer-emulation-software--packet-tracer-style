namespace NetSim.Application.Canvas;

/// <summary>
/// Pure geometry for connection anchor points: where each of a device's interface connection
/// points sits, in world coordinates, given only the device's canvas rectangle and how many
/// interfaces it has. There is no per-device hardcoding - anchors are distributed evenly along
/// the device's bottom edge, so the layout holds for a one-port PC and an eight-port switch
/// alike, and every anchor moves rigidly with the device (the connection line is always derived
/// from this, never stored - see docs/architecture/connections.md).
/// </summary>
public static class InterfaceAnchors
{
    /// <summary>
    /// World-space anchor for interface <paramref name="index"/> of a device that has
    /// <paramref name="count"/> interfaces, whose canvas rectangle starts at
    /// <paramref name="deviceTopLeft"/> and is <paramref name="deviceSize"/> big.
    /// </summary>
    public static CanvasPoint ForInterface(CanvasPoint deviceTopLeft, CanvasSize deviceSize, int index, int count)
    {
        var y = deviceTopLeft.Y + deviceSize.Height;

        if (count <= 0)
        {
            return new CanvasPoint(deviceTopLeft.X + deviceSize.Width / 2, y);
        }

        var clampedIndex = index < 0 ? 0 : index >= count ? count - 1 : index;
        var slot = (clampedIndex + 0.5) / count;
        return new CanvasPoint(deviceTopLeft.X + slot * deviceSize.Width, y);
    }
}

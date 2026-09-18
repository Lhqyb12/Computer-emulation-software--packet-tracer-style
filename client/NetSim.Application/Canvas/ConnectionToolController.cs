using System;
using System.Collections.Generic;
using System.Linq;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Application.Canvas;

/// <summary>
/// Drives the Connect tool's click-to-click workflow entirely in the Application layer (no
/// Avalonia), so it is unit-testable exactly like <see cref="CanvasInteractionController"/>:
///
///   click a first interface anchor -> a rubber-band preview follows the pointer
///     -> click a second anchor -> a real <see cref="Connection"/> is created through
///        <see cref="IConnectionService"/> (which owns validation + marks the project dirty)
///     -> Escape / clicking empty space cancels, leaving no half-made connection.
///
/// The controller never draws anything and never touches <see cref="Connection"/> directly - it
/// only resolves canvas geometry to <see cref="InterfaceEndpoint"/>s and hands the two chosen
/// <see cref="NetworkInterface"/>s to the service.
/// </summary>
public sealed class ConnectionToolController
{
    private readonly IApplicationState _applicationState;
    private readonly ICanvasItemsState _itemsState;
    private readonly IConnectionService _connectionService;

    public ConnectionToolController(
        IApplicationState applicationState,
        ICanvasItemsState itemsState,
        IConnectionService connectionService)
    {
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(connectionService);

        _applicationState = applicationState;
        _itemsState = itemsState;
        _connectionService = connectionService;
    }

    /// <summary>The first interface the user picked, or null when no cable is in progress.</summary>
    public InterfaceEndpoint? PendingEndpoint { get; private set; }

    /// <summary>The interface anchor currently under the pointer, or null.</summary>
    public InterfaceEndpoint? HoveredEndpoint { get; private set; }

    /// <summary>Last known pointer position in world coordinates - the free end of the preview line.</summary>
    public CanvasPoint PointerWorldPosition { get; private set; }

    /// <summary>User-facing message for the last action (a prompt, or why a connection was rejected).</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>Id of the connection created by the most recent successful pick pair, if any.</summary>
    public EntityId? LastCreatedConnectionId { get; private set; }

    public bool HasPendingEndpoint => PendingEndpoint is not null;

    /// <summary>Raised after any state change so the canvas can redraw and the shell can surface <see cref="StatusMessage"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Every interface anchor currently on the canvas, resolved against the live network. Used
    /// both for hit-testing and by the view to draw the anchor points / compatible-target
    /// highlighting.
    /// </summary>
    public IReadOnlyList<InterfaceEndpoint> InterfaceEndpoints()
    {
        var network = _applicationState.CurrentNetwork;
        if (network is null)
        {
            return Array.Empty<InterfaceEndpoint>();
        }

        var result = new List<InterfaceEndpoint>();
        foreach (var item in _itemsState.Items)
        {
            var device = network.Devices.FirstOrDefault(d => d.Id == item.Id);
            if (device is null)
            {
                continue;
            }

            var interfaces = device.Interfaces.ToList();
            for (var i = 0; i < interfaces.Count; i++)
            {
                var networkInterface = interfaces[i];
                var anchor = InterfaceAnchors.ForInterface(item.Position, item.Size, i, interfaces.Count);
                result.Add(new InterfaceEndpoint(
                    device.Id,
                    i,
                    networkInterface.Name,
                    networkInterface.ShortName,
                    networkInterface.InterfaceType,
                    networkInterface.Capabilities,
                    networkInterface.Speed,
                    networkInterface.AdministrativeState,
                    networkInterface.OperationalState,
                    anchor,
                    networkInterface.IsConnected));
            }
        }

        return result;
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is a valid target for the interface picked first
    /// (<see cref="PendingEndpoint"/>): it must be a different interface, available (enabled and
    /// not already connected) and media-compatible. Returns false when no cable is in progress.
    /// The UI calls this for compatible-target highlighting so it never re-implements the rule.
    /// </summary>
    public bool IsCompatibleWithPending(InterfaceEndpoint candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (PendingEndpoint is null || candidate.SameInterfaceAs(PendingEndpoint))
        {
            return false;
        }

        if (!candidate.IsAvailable || !PendingEndpoint.IsEnabled)
        {
            return false;
        }

        return ConnectionCompatibility.AreCompatible(PendingEndpoint.InterfaceType, candidate.InterfaceType);
    }

    private static string DescribeIncompatibility(InterfaceEndpoint pending, InterfaceEndpoint target)
    {
        if (target.IsConnected)
        {
            return $"Interface {target.InterfaceName} is already connected.";
        }

        if (!target.IsEnabled)
        {
            return $"Interface {target.InterfaceName} is administratively disabled.";
        }

        if (!ConnectionCompatibility.AreCompatible(pending.InterfaceType, target.InterfaceType, out var reason))
        {
            return reason ?? "These interfaces are not compatible.";
        }

        return "Please select a different interface.";
    }

    /// <summary>Nearest interface anchor within <paramref name="radius"/> world units of <paramref name="worldPoint"/>, or null.</summary>
    public InterfaceEndpoint? HitTest(CanvasPoint worldPoint, double radius)
    {
        InterfaceEndpoint? best = null;
        var bestDistanceSquared = radius * radius;

        foreach (var endpoint in InterfaceEndpoints())
        {
            var dx = endpoint.Anchor.X - worldPoint.X;
            var dy = endpoint.Anchor.Y - worldPoint.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = endpoint;
            }
        }

        return best;
    }

    public void OnPointerMoved(CanvasPoint worldPoint, double hitRadius)
    {
        PointerWorldPosition = worldPoint;
        HoveredEndpoint = HitTest(worldPoint, hitRadius);
        Raise();
    }

    /// <summary>Handles a left click at <paramref name="worldPoint"/> while the Connect tool is active.</summary>
    public void OnClick(CanvasPoint worldPoint, double hitRadius)
    {
        PointerWorldPosition = worldPoint;
        var target = HitTest(worldPoint, hitRadius);
        LastCreatedConnectionId = null;

        if (target is null)
        {
            // A click on empty canvas abandons an in-progress cable rather than doing nothing.
            CancelInternal(raise: PendingEndpoint is not null || StatusMessage is not null);
            return;
        }

        if (PendingEndpoint is null)
        {
            if (target.IsConnected)
            {
                StatusMessage = $"Interface {target.InterfaceName} is already connected.";
                Raise();
                return;
            }

            if (!target.IsEnabled)
            {
                StatusMessage = $"Interface {target.InterfaceName} is administratively disabled.";
                Raise();
                return;
            }

            PendingEndpoint = target;
            StatusMessage = $"Select a compatible interface to connect {target.InterfaceName} to.";
            Raise();
            return;
        }

        if (!IsCompatibleWithPending(target))
        {
            StatusMessage = DescribeIncompatibility(PendingEndpoint, target);
            Raise();
            return;
        }

        if (target.SameInterfaceAs(PendingEndpoint))
        {
            // Clicking the same anchor again is a natural "never mind".
            CancelInternal(raise: true);
            return;
        }

        TryConnect(PendingEndpoint, target);
    }

    /// <summary>Clears any in-progress cable and status. Safe to call at any time (e.g. on Escape or tool switch).</summary>
    public void Cancel() => CancelInternal(raise: PendingEndpoint is not null || StatusMessage is not null || HoveredEndpoint is not null);

    private void CancelInternal(bool raise)
    {
        PendingEndpoint = null;
        HoveredEndpoint = null;
        StatusMessage = null;
        if (raise)
        {
            Raise();
        }
    }

    private void TryConnect(InterfaceEndpoint a, InterfaceEndpoint b)
    {
        var network = _applicationState.CurrentNetwork;
        if (network is null)
        {
            StatusMessage = "No active network.";
            PendingEndpoint = null;
            Raise();
            return;
        }

        var interfaceA = network.Devices.FirstOrDefault(d => d.Id == a.DeviceId)?.Interfaces.ElementAtOrDefault(a.InterfaceIndex);
        var interfaceB = network.Devices.FirstOrDefault(d => d.Id == b.DeviceId)?.Interfaces.ElementAtOrDefault(b.InterfaceIndex);

        if (interfaceA is null || interfaceB is null)
        {
            StatusMessage = "One of the selected interfaces no longer exists.";
            PendingEndpoint = null;
            Raise();
            return;
        }

        var connectionType = ConnectionCompatibility.InferConnectionType(interfaceA.InterfaceType, interfaceB.InterfaceType);

        try
        {
            var result = _connectionService.Connect(interfaceA, interfaceB, connectionType);
            StatusMessage = result.IsSuccess ? null : result.ErrorMessage;
            LastCreatedConnectionId = result.IsSuccess ? result.Value!.Id : null;
        }
        catch (DomainException ex)
        {
            // Domain-rule violations (self-connection, already connected, incompatible media,
            // duplicate) surface from Network/Connection as exceptions - turn them into a
            // friendly status message instead of letting them escape to the UI.
            StatusMessage = ex.Message;
        }

        PendingEndpoint = null;
        Raise();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

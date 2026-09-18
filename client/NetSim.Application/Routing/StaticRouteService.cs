using System.Linq;
using NetSim.Application.Common;
using NetSim.Application.State;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Application.Routing;

/// <summary>
/// Default <see cref="IStaticRouteService"/>. A thin, stateless coordinator over each
/// <see cref="Router.RoutingTable"/>: it validates user input, translates it into a Core
/// <see cref="Route"/>, and adds / replaces / removes it through the table's own
/// <see cref="IRoutingTable.AddRoute"/> / <see cref="IRoutingTable.RemoveRoute"/> API (never by
/// reaching into table internals). The routing engine and <see cref="RouteResolver"/> are reused
/// for "is this route usable right now" - this service adds no routing logic of its own.
/// </summary>
public sealed class StaticRouteService : IStaticRouteService
{
    private readonly IApplicationState _applicationState;

    public StaticRouteService(IApplicationState applicationState)
    {
        ArgumentNullException.ThrowIfNull(applicationState);
        _applicationState = applicationState;
    }

    public event EventHandler? StaticRoutesChanged;

    public IReadOnlyList<StaticRouteView> GetStaticRoutes(Router router)
    {
        ArgumentNullException.ThrowIfNull(router);
        router.SyncConnectedRoutes();

        var allRoutes = router.RoutingTable.GetRoutes();
        return allRoutes
            .Where(IsManaged)
            .Select(route => new StaticRouteView(route, route.OutgoingInterface?.ShortName, StatusOf(router, route)))
            .ToList();
    }

    public StaticRouteValidationResult ValidateStaticRoute(Router router, StaticRouteInput input, Guid? existingRouteId = null)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(input);
        return TryBuildRoute(router, input, existingRouteId, out _, out var error)
            ? StaticRouteValidationResult.Valid
            : StaticRouteValidationResult.Invalid(error!);
    }

    public OperationResult<StaticRouteView> AddStaticRoute(Router router, StaticRouteInput input)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(input);

        if (_applicationState.CurrentNetwork is null)
        {
            return OperationResult<StaticRouteView>.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!TryBuildRoute(router, input, existingRouteId: null, out var route, out var error))
        {
            return OperationResult<StaticRouteView>.Failure(OperationErrorType.ValidationError, error!);
        }

        try
        {
            router.RoutingTable.AddRoute(route!);
        }
        catch (DomainException ex)
        {
            return OperationResult<StaticRouteView>.Failure(OperationErrorType.ValidationError, ex.Message);
        }

        Committed();
        return OperationResult<StaticRouteView>.Success(
            new StaticRouteView(route!, route!.OutgoingInterface?.ShortName, StatusOf(router, route!)));
    }

    public OperationResult<StaticRouteView> UpdateStaticRoute(Router router, Guid routeId, StaticRouteInput input)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(input);

        if (FindManaged(router, routeId) is not { } existing)
        {
            return OperationResult<StaticRouteView>.Failure(
                OperationErrorType.NotFound, "That static route no longer exists.");
        }

        if (!TryBuildRoute(router, input, existingRouteId: routeId, out var replacement, out var error))
        {
            return OperationResult<StaticRouteView>.Failure(OperationErrorType.ValidationError, error!);
        }

        // Remove the old entry (its prefix may be changing) then add the replacement, which keeps
        // the same Id. On failure, put the original back so the table is never left short a route.
        router.RoutingTable.RemoveRoute(existing.Destination);
        try
        {
            router.RoutingTable.AddRoute(replacement!);
        }
        catch (DomainException ex)
        {
            router.RoutingTable.AddRoute(existing);
            return OperationResult<StaticRouteView>.Failure(OperationErrorType.ValidationError, ex.Message);
        }

        Committed();
        return OperationResult<StaticRouteView>.Success(
            new StaticRouteView(replacement!, replacement!.OutgoingInterface?.ShortName, StatusOf(router, replacement!)));
    }

    public OperationResult RemoveStaticRoute(Router router, Guid routeId)
    {
        ArgumentNullException.ThrowIfNull(router);

        if (FindManaged(router, routeId) is not { } route)
        {
            return OperationResult.Failure(OperationErrorType.NotFound, "That static route no longer exists.");
        }

        router.RoutingTable.RemoveRoute(route.Destination);
        Committed();
        return OperationResult.Success();
    }

    // ---- internals --------------------------------------------------------------------------

    private static bool IsManaged(Route route) => route.Type is RouteType.Static or RouteType.Default;

    private static Route? FindManaged(Router router, Guid routeId) =>
        router.RoutingTable.GetRoutes().FirstOrDefault(r => IsManaged(r) && r.Id == routeId);

    private void Committed()
    {
        _applicationState.MarkCurrentProjectDirty();
        StaticRoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static StaticRouteStatus StatusOf(Router router, Route route)
    {
        var resolution = RouteResolver.Resolve(router.RoutingTable.GetRoutes(), route, route.Destination.NetworkAddress);
        if (resolution is not null)
        {
            return StaticRouteStatus.Active;
        }

        if (route.OutgoingInterface is { IsOperational: false })
        {
            return StaticRouteStatus.InterfaceDown;
        }

        return route.NextHop is not null ? StaticRouteStatus.UnreachableNextHop : StaticRouteStatus.Inactive;
    }

    /// <summary>
    /// Validates <paramref name="input"/> and, on success, produces the Core <see cref="Route"/> it
    /// describes (with <paramref name="existingRouteId"/> preserved when editing). Returns false with
    /// a user-facing <paramref name="error"/> otherwise.
    /// </summary>
    private static bool TryBuildRoute(
        Router router, StaticRouteInput input, Guid? existingRouteId, out Route? route, out string? error)
    {
        route = null;
        error = null;

        if (!IPv4Address.TryParse((input.DestinationNetwork ?? string.Empty).Trim(), out var networkAddress))
        {
            error = "Enter a valid destination network address, e.g. 192.168.2.0.";
            return false;
        }

        if (input.PrefixLength is < 0 or > 32)
        {
            error = "Prefix length must be between 0 and 32.";
            return false;
        }

        var destination = IPv4Network.Create(networkAddress, input.PrefixLength);

        var nextHopText = string.IsNullOrWhiteSpace(input.NextHop) ? null : input.NextHop!.Trim();
        var interfaceText = string.IsNullOrWhiteSpace(input.OutgoingInterfaceName) ? null : input.OutgoingInterfaceName!.Trim();

        if (nextHopText is null && interfaceText is null)
        {
            error = "A static route needs a next hop, an outgoing interface, or both.";
            return false;
        }

        IPv4Address? nextHop = null;
        if (nextHopText is not null)
        {
            if (!IPv4Address.TryParse(nextHopText, out var parsedNextHop))
            {
                error = $"'{nextHopText}' is not a valid IPv4 next-hop address.";
                return false;
            }

            if (parsedNextHop.IsUnspecified || parsedNextHop.IsLimitedBroadcast || parsedNextHop.IsMulticast)
            {
                error = $"'{parsedNextHop}' is not a usable next-hop address.";
                return false;
            }

            if (RouterOwns(router, parsedNextHop))
            {
                error = "The next hop cannot be one of this router's own interface addresses.";
                return false;
            }

            nextHop = parsedNextHop;
        }

        NetworkInterface? outgoingInterface = null;
        if (interfaceText is not null)
        {
            outgoingInterface = router.Interfaces.FirstOrDefault(
                i => string.Equals(i.Name, interfaceText, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(i.ShortName, interfaceText, StringComparison.OrdinalIgnoreCase));
            if (outgoingInterface is null)
            {
                error = $"Interface '{interfaceText}' does not exist on this router.";
                return false;
            }
        }

        // A fully specified route must be coherent: the stated next hop has to sit on the stated
        // interface's own network.
        if (nextHop is { } nh && outgoingInterface?.PrimaryIPv4Configuration is { } cfg && !cfg.Network.Contains(nh))
        {
            error = $"Next hop {nh} is not on interface {outgoingInterface.ShortName}'s network ({cfg.Network}).";
            return false;
        }

        // One configured route per destination prefix (the table is keyed that way). A different
        // route already on that prefix is a duplicate; a connected route there is off-limits.
        var clash = router.RoutingTable.GetRoutes().FirstOrDefault(r => r.Destination == destination);
        if (clash is not null && clash.Id != existingRouteId)
        {
            error = clash.Type == RouteType.Connected
                ? $"{destination} is a directly connected network and cannot have a static route."
                : $"A static route to {destination} already exists. Edit it instead.";
            return false;
        }

        var metric = input.Metric is { } m && m >= 0 ? m : 0;
        var routeType = input.PrefixLength == 0 ? RouteType.Default : RouteType.Static;
        var adminDistance = input.AdministrativeDistance is { } ad && ad is >= 0 and <= 255 ? ad : (int?)null;

        route = Route.Create(
            destination, routeType, outgoingInterface, nextHop, metric, adminDistance,
            id: existingRouteId ?? Guid.NewGuid());
        return true;
    }

    private static bool RouterOwns(Router router, IPv4Address address) =>
        router.Interfaces.SelectMany(i => i.IPv4Configurations).Any(c => c.Address == address);
}

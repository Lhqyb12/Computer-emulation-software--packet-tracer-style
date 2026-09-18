using NetSim.Application.Common;
using NetSim.Core.Devices;

namespace NetSim.Application.Routing;

/// <summary>
/// The single door for managing a router's manually-configured static routes (Phase 28). The UI
/// never touches <see cref="Core.Routing.IRoutingTable"/> directly - every add / edit / remove
/// goes through here so validation, the project dirty flag and the change notification all happen
/// in one place, exactly like <see cref="Services.IDeviceService"/> for interface configuration.
///
/// <para>Connected routes are <em>not</em> in scope: they are derived from interface addressing
/// and can never be added, edited or removed as static routes.</para>
/// </summary>
public interface IStaticRouteService
{
    /// <summary>Every manually configured route (static and default) on <paramref name="router"/>, with its current usability.</summary>
    IReadOnlyList<StaticRouteView> GetStaticRoutes(Router router);

    /// <summary>
    /// Validates <paramref name="input"/> against <paramref name="router"/> without changing
    /// anything. <paramref name="existingRouteId"/>, when given, is the route being edited - it is
    /// excluded from the duplicate check.
    /// </summary>
    StaticRouteValidationResult ValidateStaticRoute(Router router, StaticRouteInput input, Guid? existingRouteId = null);

    /// <summary>
    /// Adds a static route to <paramref name="router"/>. Fails with
    /// <see cref="OperationErrorType.ValidationError"/> for invalid input or a duplicate prefix, and
    /// <see cref="OperationErrorType.InvalidState"/> when no project/network is open. On success the
    /// project is marked dirty and <see cref="StaticRoutesChanged"/> is raised.
    /// </summary>
    OperationResult<StaticRouteView> AddStaticRoute(Router router, StaticRouteInput input);

    /// <summary>
    /// Replaces the configuration of the route identified by <paramref name="routeId"/> on
    /// <paramref name="router"/>. Fails with <see cref="OperationErrorType.NotFound"/> when no such
    /// static route exists, and <see cref="OperationErrorType.ValidationError"/> for invalid input.
    /// The route keeps its identity across a destination change.
    /// </summary>
    OperationResult<StaticRouteView> UpdateStaticRoute(Router router, Guid routeId, StaticRouteInput input);

    /// <summary>
    /// Removes the static route identified by <paramref name="routeId"/> from <paramref name="router"/>.
    /// Fails with <see cref="OperationErrorType.NotFound"/> when it is not a configured static route
    /// (a connected route can never be removed this way).
    /// </summary>
    OperationResult RemoveStaticRoute(Router router, Guid routeId);

    /// <summary>Raised after any static route is added, edited or removed through this service.</summary>
    event EventHandler? StaticRoutesChanged;
}

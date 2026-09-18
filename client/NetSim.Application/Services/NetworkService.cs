using NetSim.Application.State;
using NetSim.Core.Topology;

namespace NetSim.Application.Services;

public sealed class NetworkService : INetworkService
{
    private readonly IApplicationState _applicationState;

    // Optional so the many existing "new NetworkService(new ApplicationState())" call sites
    // (tests, composition) keep working; the query service is stateless, so a default instance
    // is as good as an injected one. DI still registers and injects the shared instance.
    public NetworkService(IApplicationState applicationState, ITopologyQueryService? topologyQuery = null)
    {
        _applicationState = applicationState;
        TopologyQuery = topologyQuery ?? new TopologyQueryService();
    }

    public Network? CurrentNetwork => _applicationState.CurrentNetwork;

    public ITopologyQueryService TopologyQuery { get; }

    public Network CreateNetwork(string name)
    {
        // Name validation (null/whitespace) is a domain rule already enforced by the
        // Network constructor via Guard - no need to duplicate it here.
        var network = new Network(name);
        _applicationState.SetCurrentNetwork(network);
        return network;
    }

    public void ClearNetwork() => _applicationState.SetCurrentNetwork(null);
}

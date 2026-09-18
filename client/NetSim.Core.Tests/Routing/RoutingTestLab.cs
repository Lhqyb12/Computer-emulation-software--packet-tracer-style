using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Routing;

/// <summary>
/// Small fixture builder for the routing unit tests: a bare <see cref="Router"/> (no default
/// interfaces - <c>new Router</c> is deliberately minimal) with the interfaces the test needs,
/// each Ethernet-capable, administratively enabled and brought operationally up.
/// </summary>
internal sealed class RoutingTestLab
{
    private RoutingTestLab(Router router, NetworkInterface gi0, NetworkInterface? gi1)
    {
        Router = router;
        Gi0 = gi0;
        Gi1 = gi1!;
    }

    public Router Router { get; }

    public NetworkInterface Gi0 { get; }

    public NetworkInterface Gi1 { get; }

    public static RoutingTestLab WithOneInterface(string? cidr = null)
    {
        var router = new Router("R1");
        var gi0 = AddUpInterface(router, "GigabitEthernet0/0");
        if (cidr is not null)
        {
            Configure(gi0, cidr);
        }

        router.SyncConnectedRoutes();
        return new RoutingTestLab(router, gi0, null);
    }

    public static RoutingTestLab WithTwoInterfaces(string? cidr0 = null, string? cidr1 = null)
    {
        var router = new Router("R1");
        var gi0 = AddUpInterface(router, "GigabitEthernet0/0");
        var gi1 = AddUpInterface(router, "GigabitEthernet0/1");
        if (cidr0 is not null)
        {
            Configure(gi0, cidr0);
        }

        if (cidr1 is not null)
        {
            Configure(gi1, cidr1);
        }

        router.SyncConnectedRoutes();
        return new RoutingTestLab(router, gi0, gi1);
    }

    public static void Configure(NetworkInterface networkInterface, string cidr)
    {
        var slash = cidr.IndexOf('/');
        var address = IPv4Address.Parse(cidr[..slash]);
        var prefix = int.Parse(cidr[(slash + 1)..]);
        networkInterface.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(address, prefix));
        if (networkInterface.Device is Router router)
        {
            router.SyncConnectedRoutes();
        }
    }

    private static NetworkInterface AddUpInterface(Router router, string name)
    {
        var networkInterface = router.AddInterface(name, InterfaceType.GigabitEthernet);
        networkInterface.BringUp();
        return networkInterface;
    }
}

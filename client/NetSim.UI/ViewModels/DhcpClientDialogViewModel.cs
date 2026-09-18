using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Dhcp;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The DHCP client tool (brief section 48): requests, renews and releases a DHCP lease for
/// <paramref name="_sourceInterface"/> and shows the resulting network configuration. Same
/// stays-open, self-contained, off-the-UI-thread shape as <see cref="PingDialogViewModel"/> /
/// <see cref="DnsLookupDialogViewModel"/> - all work goes through <see cref="IDhcpClient"/>, never
/// the operating system's DHCP.
/// </summary>
public partial class DhcpClientDialogViewModel : ViewModelBase
{
    private readonly IDhcpClient _dhcpClient;
    private readonly IDhcpClientStateStore _stateStore;
    private readonly NetworkInterface _sourceInterface;
    private readonly Action _onClose;

    [ObservableProperty]
    private string _modeText = "Static";

    [ObservableProperty]
    private string _statusText = "Init";

    [ObservableProperty]
    private string _addressText = "-";

    [ObservableProperty]
    private string _subnetText = "-";

    [ObservableProperty]
    private string _gatewayText = "-";

    [ObservableProperty]
    private string _dnsText = "-";

    [ObservableProperty]
    private string _leaseText = "-";

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _hasRun;

    public DhcpClientDialogViewModel(
        NetworkInterface sourceInterface, IDhcpClient dhcpClient, IDhcpClientStateStore stateStore, Action onClose)
    {
        _sourceInterface = sourceInterface;
        _dhcpClient = dhcpClient;
        _stateStore = stateStore;
        _onClose = onClose;
        RefreshFromState();
    }

    public string SourceDescription => $"{_sourceInterface.Device.Name} / {_sourceInterface.ShortName}";

    [RelayCommand]
    private Task RequestLease() => ExecuteAsync(async () =>
    {
        Message = "Requesting a DHCP lease...";
        HasRun = true;

        var result = await Task.Run(() => _dhcpClient.Acquire(_sourceInterface)).ConfigureAwait(true);
        Message = result.IsSuccess ? $"Bound to {result.Address}." : $"{result.Status}: {result.FailureReason}";
        RefreshFromState();
    });

    [RelayCommand]
    private Task Renew() => ExecuteAsync(async () =>
    {
        Message = "Renewing the DHCP lease...";
        var result = await Task.Run(() => _dhcpClient.Renew(_sourceInterface)).ConfigureAwait(true);
        Message = result.IsSuccess ? $"Lease renewed for {result.Address}." : $"{result.Status}: {result.FailureReason}";
        RefreshFromState();
    });

    [RelayCommand]
    private Task Release() => ExecuteAsync(async () =>
    {
        var result = await Task.Run(() => _dhcpClient.Release(_sourceInterface)).ConfigureAwait(true);
        Message = result.Detail;
        RefreshFromState();
    });

    [RelayCommand]
    private void Close() => _onClose();

    private void RefreshFromState()
    {
        if (!_stateStore.TryGet(_sourceInterface, out var binding) || !binding.IsDhcpManaged)
        {
            ModeText = "Static";
            StatusText = "Not using DHCP";
            AddressText = _sourceInterface.IPv4Address?.ToString() ?? "-";
            SubnetText = _sourceInterface.PrimaryIPv4Configuration?.SubnetMask.ToString() ?? "-";
            GatewayText = "-";
            DnsText = "-";
            LeaseText = "-";
            return;
        }

        ModeText = "DHCP";
        StatusText = binding.State.ToString();

        if (binding.Configuration is { } configuration)
        {
            AddressText = configuration.Address.ToString();
            SubnetText = configuration.SubnetMask.ToString();
            GatewayText = configuration.Gateway?.ToString() ?? "(none)";
            DnsText = configuration.DnsServer?.ToString() ?? "(none)";
            LeaseText = $"{(int)configuration.LeaseDuration.TotalSeconds}s (until {configuration.LeaseExpiration:u})";
        }
        else
        {
            AddressText = "-";
            SubnetText = "-";
            GatewayText = "-";
            DnsText = "-";
            LeaseText = "-";
        }
    }
}

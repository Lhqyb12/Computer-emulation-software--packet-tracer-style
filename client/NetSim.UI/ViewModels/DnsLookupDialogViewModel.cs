using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Dns;
using NetSim.Core.Dns;
using NetSim.Core.Networking;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The DNS Lookup diagnostic tool (brief section 40): lets the user resolve a domain name typed
/// against <paramref name="_sourceInterface"/>'s device's configured DNS server and shows the
/// result. Mirrors <see cref="PingDialogViewModel"/>'s shape (stays open across several lookups,
/// self-contained, runs off the UI thread) - all resolution goes through <see cref="IDnsResolver"/>,
/// never the operating system's real DNS.
/// </summary>
public partial class DnsLookupDialogViewModel : ViewModelBase
{
    private readonly IDnsResolver _dnsResolver;
    private readonly IDnsClientConfigurationStore _dnsConfiguration;
    private readonly NetworkInterface _sourceInterface;
    private readonly Action _onClose;

    [ObservableProperty]
    private string _domainInput = string.Empty;

    [ObservableProperty]
    private DnsRecordType _selectedRecordType = DnsRecordType.A;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _hasRun;

    public DnsLookupDialogViewModel(
        NetworkInterface sourceInterface, IDnsResolver dnsResolver, IDnsClientConfigurationStore dnsConfiguration, Action onClose)
    {
        _sourceInterface = sourceInterface;
        _dnsResolver = dnsResolver;
        _dnsConfiguration = dnsConfiguration;
        _onClose = onClose;
    }

    public string SourceDescription => $"{_sourceInterface.Device.Name} / {_sourceInterface.ShortName}";

    /// <summary>The device's configured DNS server(s), shown so the user knows where the lookup is sent. "(none configured)" otherwise.</summary>
    public string DnsServerText
    {
        get
        {
            var servers = _dnsConfiguration.GetServers(_sourceInterface.Device);
            return servers.Count == 0 ? "(no DNS server configured)" : string.Join(", ", servers);
        }
    }

    public DnsRecordType[] RecordTypes { get; } = [DnsRecordType.A, DnsRecordType.AAAA, DnsRecordType.CNAME, DnsRecordType.NS, DnsRecordType.MX];

    [RelayCommand]
    private Task Lookup() => ExecuteAsync(async () =>
    {
        ValidationError = null;

        if (!DomainName.TryParse(DomainInput.Trim(), out var name) || name.IsRoot)
        {
            ValidationError = $"'{DomainInput}' is not a valid domain name.";
            return;
        }

        HasRun = true;
        ResultText = $"Resolving {name} {SelectedRecordType}...";

        var type = SelectedRecordType;
        var result = await Task.Run(() => _dnsResolver.Resolve(_sourceInterface, name, type)).ConfigureAwait(true);

        ResultText = result.IsSuccess
            ? string.Join(Environment.NewLine, result.Records.Select(r => $"{r.Name}  {r.Type}  {r.DataText}  (TTL {(int)r.Ttl.TotalSeconds}s)"))
                + (result.FromCache ? Environment.NewLine + "(from cache)" : string.Empty)
            : $"{result.Status}: {result.ErrorMessage}";
    });

    [RelayCommand]
    private void Close() => _onClose();
}

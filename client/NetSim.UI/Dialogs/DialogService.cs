using System.Threading.Tasks;
using NetSim.Application.Dhcp;
using NetSim.Application.Diagnostics;
using NetSim.Application.Dns;
using NetSim.Core.Dhcp;
using NetSim.Core.Dns;
using NetSim.Core.Networking;
using NetSim.UI.ViewModels;

namespace NetSim.UI.Dialogs;

public sealed class DialogService : IDialogService
{
    private readonly IDialogHost _dialogHost;
    private readonly IPingService _pingService;
    private readonly IDnsResolver _dnsResolver;
    private readonly IDnsClientConfigurationStore _dnsConfiguration;
    private readonly IDhcpClient _dhcpClient;
    private readonly IDhcpClientStateStore _dhcpClientState;

    public DialogService(
        IDialogHost dialogHost, IPingService pingService, IDnsResolver dnsResolver, IDnsClientConfigurationStore dnsConfiguration,
        IDhcpClient dhcpClient, IDhcpClientStateStore dhcpClientState)
    {
        _dialogHost = dialogHost;
        _pingService = pingService;
        _dnsResolver = dnsResolver;
        _dnsConfiguration = dnsConfiguration;
        _dhcpClient = dhcpClient;
        _dhcpClientState = dhcpClientState;
    }

    public Task<NewProjectDialogResult?> ShowNewProjectAsync()
    {
        var tcs = new TaskCompletionSource<NewProjectDialogResult?>();

        var dialog = new NewProjectDialogViewModel(
            onCreate: result => Complete(tcs, result),
            onCancel: () => Complete(tcs, null));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task<string?> ShowRenameProjectAsync(string currentName)
    {
        var tcs = new TaskCompletionSource<string?>();

        var dialog = new RenameProjectDialogViewModel(
            currentName,
            onRename: name => Complete(tcs, name),
            onCancel: () => Complete(tcs, null));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new ConfirmationDialogViewModel(
            title,
            message,
            confirmText,
            cancelText,
            isDestructive,
            onConfirm: () => Complete(tcs, true),
            onCancel: () => Complete(tcs, false));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task<UnsavedChangesDecision> ShowUnsavedChangesAsync(string projectName)
    {
        var tcs = new TaskCompletionSource<UnsavedChangesDecision>();

        var dialog = new UnsavedChangesDialogViewModel(
            projectName,
            onDecision: decision => Complete(tcs, decision));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task ShowPingAsync(NetworkInterface sourceInterface)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new PingDialogViewModel(
            sourceInterface,
            _pingService,
            onClose: () => Complete(tcs, true));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task ShowDnsLookupAsync(NetworkInterface sourceInterface)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new DnsLookupDialogViewModel(
            sourceInterface,
            _dnsResolver,
            _dnsConfiguration,
            onClose: () => Complete(tcs, true));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    public Task ShowDhcpAsync(NetworkInterface sourceInterface)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new DhcpClientDialogViewModel(
            sourceInterface,
            _dhcpClient,
            _dhcpClientState,
            onClose: () => Complete(tcs, true));

        _dialogHost.ActiveDialog = dialog;
        return tcs.Task;
    }

    private void Complete<T>(TaskCompletionSource<T> tcs, T result)
    {
        _dialogHost.ActiveDialog = null;
        tcs.TrySetResult(result);
    }
}

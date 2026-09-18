using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Diagnostics;
using NetSim.Core.Networking;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The Ping diagnostic tool (brief section 33): lets the user send a batch of simulated Echo
/// Requests from <paramref name="_sourceInterface"/> to a destination they type, and shows each
/// reply as it completes plus the aggregate Sent/Received/Lost/Loss% summary. Unlike the other
/// dialogs this one is not a single input collector - it stays open across multiple ping runs and
/// only resolves its hosting task when the user closes it (see <see cref="Dialogs.IDialogService.ShowPingAsync"/>).
///
/// All simulation work goes through <see cref="IPingService"/> (never the operating system's real
/// network) and runs off the UI thread via <see cref="ViewModelBase.ExecuteAsync"/> +
/// <see cref="Task.Run(Action)"/> so a multi-reply ping never blocks the UI (brief section 49).
/// </summary>
public partial class PingDialogViewModel : ViewModelBase
{
    private const int PingCount = 4;

    private readonly IPingService _pingService;
    private readonly NetworkInterface _sourceInterface;
    private readonly Action _onClose;

    [ObservableProperty]
    private string _destinationInput = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private bool _hasRun;

    public PingDialogViewModel(NetworkInterface sourceInterface, IPingService pingService, Action onClose)
    {
        _sourceInterface = sourceInterface;
        _pingService = pingService;
        _onClose = onClose;
    }

    /// <summary>The interface's own address, shown so the user knows where the ping originates from.</summary>
    public string SourceAddressText => _sourceInterface.IPv4Address?.ToString() ?? "(no IPv4 address)";

    public string SourceDescription => $"{_sourceInterface.Device.Name} / {_sourceInterface.ShortName}";

    public ObservableCollection<PingReplyRow> Replies { get; } = [];

    [RelayCommand]
    private Task Ping() => ExecuteAsync(async () =>
    {
        ValidationError = null;

        if (!IPv4Address.TryParse(DestinationInput, out var destination))
        {
            ValidationError = $"'{DestinationInput}' is not a valid IPv4 address.";
            return;
        }

        Replies.Clear();
        SummaryText = $"Pinging {destination} with {PingCount} echo requests...";
        HasRun = true;

        var result = await Task.Run(() => _pingService.Ping(_sourceInterface, destination, PingCount)).ConfigureAwait(true);

        foreach (var reply in result.Replies)
        {
            Replies.Add(new PingReplyRow(reply));
        }

        SummaryText =
            $"Packets: Sent = {result.PacketsSent}, Received = {result.PacketsReceived}, Lost = {result.PacketsLost} " +
            $"({result.PacketLossPercentage:0}% loss)" +
            (result.AverageRoundTripTime is { } avg ? $", Average time = {avg.TotalMilliseconds:0}ms" : string.Empty);
    });

    [RelayCommand]
    private void Close() => _onClose();
}

/// <summary>UI-friendly projection of one <see cref="PingReply"/> - a single "Reply from ..." (or failure) line.</summary>
public sealed class PingReplyRow
{
    public PingReplyRow(PingReply reply)
    {
        IsSuccess = reply.IsSuccess;
        Text = reply.ToString();
    }

    public bool IsSuccess { get; }

    public string Text { get; }
}

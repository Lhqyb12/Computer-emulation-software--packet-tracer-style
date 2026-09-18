using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Canvas;
using NetSim.Application.Dns;
using NetSim.Application.Routing;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Networking;
using NetSim.Core.Routing;
using NetSim.Core.Switching;
using NetSim.Core.Vlans;
using NetSim.UI.Common;
using NetSim.UI.Devices;
using NetSim.UI.Dialogs;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The Device Properties panel: mirrors whichever single device is currently selected on the
/// Network Canvas (<see cref="ICanvasSelectionState"/> - the same id-based selection the canvas
/// itself uses for click/marquee selection, not the unrelated single-device
/// <see cref="ISelectionState"/> placeholder from Phase 7, which nothing on the canvas writes to)
/// and lets the user inspect it and edit the properties the current device model actually
/// supports (name, power state, and - Phase 14 - per-interface administrative state). Renaming/
/// power/interface changes go through the existing <see cref="IDeviceService"/> and mark the
/// project dirty via <see cref="IApplicationState"/> - the same doors every other screen uses -
/// rather than mutating <c>NetworkDevice</c> directly from this ViewModel. See
/// docs/architecture/device-model.md and docs/architecture/ports-and-interfaces.md.
/// </summary>
public partial class DevicePropertiesViewModel : ViewModelBase
{
    private const int MaxNameLength = 64;

    private readonly IApplicationState _applicationState;
    private readonly ICanvasSelectionState _canvasSelectionState;
    private readonly ICanvasItemsState _itemsState;
    private readonly IDeviceService _deviceService;
    private readonly IConnectionService _connectionService;
    private readonly IDialogService _dialogService;
    private readonly IDnsClientConfigurationStore _dnsConfiguration;
    private readonly ISimulationClock _simulationClock;
    private readonly IStaticRouteService _staticRouteService;

    private NetworkDevice? _selectedDevice;
    private string _originalName = string.Empty;
    private string? _selectedInterfaceName;

    [ObservableProperty]
    private DevicePropertiesSelectionMode _selectionMode = DevicePropertiesSelectionMode.None;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _deviceTypeDisplayName = string.Empty;

    [ObservableProperty]
    private string _deviceStateDisplayName = string.Empty;

    [ObservableProperty]
    private bool _isPoweredOn;

    [ObservableProperty]
    private string _deviceIdText = string.Empty;

    [ObservableProperty]
    private Geometry? _deviceIcon;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnableInterfaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableInterfaceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIPv4Command))]
    [NotifyCanExecuteChangedFor(nameof(ClearIPv4Command))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDefaultGatewayCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearDefaultGatewayCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddIPv6Command))]
    [NotifyCanExecuteChangedFor(nameof(ClearIPv6Command))]
    [NotifyCanExecuteChangedFor(nameof(ClearArpCommand))]
    [NotifyCanExecuteChangedFor(nameof(PingCommand))]
    [NotifyCanExecuteChangedFor(nameof(DnsLookupCommand))]
    [NotifyCanExecuteChangedFor(nameof(DhcpCommand))]
    private DeviceInterfaceItem? _selectedInterface;

    [ObservableProperty]
    private string _iPv4AddressInput = string.Empty;

    [ObservableProperty]
    private string _iPv4SubnetMaskInput = string.Empty;

    [ObservableProperty]
    private string? _iPv4ValidationError;

    [ObservableProperty]
    private string _defaultGatewayInput = string.Empty;

    [ObservableProperty]
    private string? _defaultGatewayValidationError;

    [ObservableProperty]
    private string _iPv6AddressInput = string.Empty;

    [ObservableProperty]
    private string _iPv6PrefixLengthInput = string.Empty;

    [ObservableProperty]
    private string? _iPv6ValidationError;

    [ObservableProperty]
    private string _dnsServerAddressInput = string.Empty;

    [ObservableProperty]
    private string? _dnsServerValidationError;

    public DevicePropertiesViewModel(
        IApplicationState applicationState,
        ICanvasSelectionState canvasSelectionState,
        ICanvasItemsState itemsState,
        IDeviceService deviceService,
        IConnectionService connectionService,
        IDialogService dialogService,
        IDnsClientConfigurationStore dnsConfiguration,
        ISimulationClock? simulationClock = null,
        IStaticRouteService? staticRouteService = null)
    {
        _applicationState = applicationState;
        _canvasSelectionState = canvasSelectionState;
        _itemsState = itemsState;
        _deviceService = deviceService;
        _connectionService = connectionService;
        _dialogService = dialogService;
        _dnsConfiguration = dnsConfiguration;
        _simulationClock = simulationClock ?? new SystemSimulationClock();
        _staticRouteService = staticRouteService ?? new StaticRouteService(applicationState);

        Interfaces = [];
        MacAddressTable = [];
        Vlans = [];
        RoutingTable = [];
        StaticRoutes = [];

        // Selection can change from the canvas (click/marquee/Escape/SelectAll), and the
        // resolved device can disappear from under us if the current network/project changes -
        // both are re-resolved through the same Refresh() so a stale device is never displayed
        // (see the Phase 12 brief, "Device Removal Safety").
        _canvasSelectionState.SelectionChanged += (_, _) => Refresh();
        _applicationState.CurrentNetworkChanged += (_, _) => Refresh();

        // Connecting/disconnecting an interface, or an interface's admin state changing, alters
        // what this panel shows for the *currently selected* device without the selection itself
        // changing - re-resolve on both (Phase 13 "Interface Status Updates" / Phase 14 Enable/Disable).
        _connectionService.ConnectionsChanged += (_, _) => Refresh();
        _deviceService.InterfacesChanged += (_, _) => Refresh();

        // Adding / editing / removing a static route (Phase 28) changes what the routing sections
        // show for the currently selected router without the selection itself changing.
        _staticRouteService.StaticRoutesChanged += (_, _) => Refresh();

        Refresh();
    }

    public ObservableCollection<DeviceInterfaceItem> Interfaces { get; }

    /// <summary>
    /// The selected switch's MAC address forwarding table (Phase 25), as read-only rows. Empty for
    /// a non-switch device and (usually) for a switch until traffic has been switched through it.
    /// Refreshed on selection and on demand via <see cref="RefreshMacTableCommand"/>.
    /// </summary>
    public ObservableCollection<SwitchMacEntryRow> MacAddressTable { get; }

    /// <summary>
    /// The selected switch's VLAN database (Phase 26), as rows the VLAN section binds to. Empty for
    /// a non-switch device; for a switch it always has at least the default VLAN (1).
    /// </summary>
    public ObservableCollection<SwitchVlanRow> Vlans { get; }

    [ObservableProperty]
    private string _newVlanIdInput = string.Empty;

    [ObservableProperty]
    private string _newVlanNameInput = string.Empty;

    [ObservableProperty]
    private string? _vlanValidationError;

    [ObservableProperty]
    private string _accessVlanInput = string.Empty;

    [ObservableProperty]
    private string? _portVlanValidationError;

    /// <summary>
    /// The selected router's IPv4 routing table (Phase 27), as read-only rows. Empty for a
    /// non-router device. Its connected routes are derived from the router's interface addressing;
    /// refreshed on selection, on any interface change, and on demand via
    /// <see cref="RefreshRoutingTableCommand"/>.
    /// </summary>
    public ObservableCollection<RouterRouteRow> RoutingTable { get; }

    /// <summary>
    /// The selected router's manually configured static and default routes (Phase 28), as editable
    /// rows. Empty for a non-router device. Managed through <see cref="IStaticRouteService"/>;
    /// refreshed on selection, on any routing change, and on demand.
    /// </summary>
    public ObservableCollection<StaticRouteRow> StaticRoutes { get; }

    [ObservableProperty]
    private string _staticRouteDestinationInput = string.Empty;

    [ObservableProperty]
    private string _staticRoutePrefixInput = string.Empty;

    [ObservableProperty]
    private string _staticRouteNextHopInput = string.Empty;

    [ObservableProperty]
    private string _staticRouteInterfaceInput = string.Empty;

    [ObservableProperty]
    private string? _staticRouteValidationError;

    [ObservableProperty]
    private Guid? _editingStaticRouteId;

    /// <summary>True when the static-route form is editing an existing route rather than adding a new one.</summary>
    public bool IsEditingStaticRoute => EditingStaticRouteId is not null;

    /// <summary>Add/Save button label for the static-route form.</summary>
    public string StaticRouteSaveButtonText => IsEditingStaticRoute ? "Save" : "Add Route";

    partial void OnEditingStaticRouteIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(IsEditingStaticRoute));
        OnPropertyChanged(nameof(StaticRouteSaveButtonText));
        CancelStaticRouteEditCommand.NotifyCanExecuteChanged();
    }

    /// <summary>True when the selected device is a switch (its MAC address table / VLAN sections are shown).</summary>
    public bool IsSwitch => _selectedDevice is Core.Devices.Switch;

    /// <summary>True when the selected device is a router (its routing table section is shown).</summary>
    public bool IsRouter => _selectedDevice is Core.Devices.Router;

    /// <summary>True when there is at least one route to display (always true for a router with a configured interface).</summary>
    public bool HasRoutingTable => RoutingTable.Count > 0;

    /// <summary>True when the selected router has at least one configured static route.</summary>
    public bool HasStaticRoutes => StaticRoutes.Count > 0;

    /// <summary>True when there is at least one MAC entry to display.</summary>
    public bool HasMacAddressTable => MacAddressTable.Count > 0;

    /// <summary>True when there is at least one VLAN to display (always true for a switch).</summary>
    public bool HasVlans => Vlans.Count > 0;

    /// <summary>True when the selected interface is a port on a switch (its VLAN mode / access-VLAN controls are shown).</summary>
    public bool SelectedInterfaceIsSwitchPort => IsSwitch && SelectedInterface is { Model.SupportsEthernet: true };

    public bool HasNoSelection => SelectionMode == DevicePropertiesSelectionMode.None;

    public bool HasMultipleSelection => SelectionMode == DevicePropertiesSelectionMode.Multiple;

    public bool HasSingleSelection => SelectionMode == DevicePropertiesSelectionMode.Single;

    public bool HasInterfaces => Interfaces.Count > 0;

    public bool HasSelectedInterface => SelectedInterface is not null;

    partial void OnDeviceNameChanged(string value) => UpdateEditingState();

    partial void OnDnsServerAddressInputChanged(string value) => ClearDnsServerCommand.NotifyCanExecuteChanged();

    partial void OnSelectionModeChanged(DevicePropertiesSelectionMode value)
    {
        OnPropertyChanged(nameof(HasNoSelection));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
    }

    partial void OnSelectedInterfaceChanged(DeviceInterfaceItem? oldValue, DeviceInterfaceItem? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        _selectedInterfaceName = newValue?.Name;

        // Seed the IPv4 editor from the interface's current primary address (blank when none), and
        // clear any stale validation message from the previously selected interface.
        var primaryIPv4 = newValue?.Model.PrimaryIPv4Configuration;
        IPv4AddressInput = primaryIPv4?.Address.ToString() ?? string.Empty;
        IPv4SubnetMaskInput = primaryIPv4?.SubnetMask.ToString() ?? string.Empty;
        IPv4ValidationError = null;

        // Seed the default-gateway editor from the interface's current value (Phase 27).
        DefaultGatewayInput = newValue?.Model.IPv4DefaultGateway?.ToString() ?? string.Empty;
        DefaultGatewayValidationError = null;

        // The IPv6 editor is an "add another address" field (an interface commonly has several),
        // so it always starts blank rather than seeded from an existing address.
        IPv6AddressInput = string.Empty;
        IPv6PrefixLengthInput = string.Empty;
        IPv6ValidationError = null;

        // Seed the switch-port VLAN editor from the port's current access VLAN (Phase 26).
        PortVlanValidationError = null;
        if (_selectedDevice is Core.Devices.Switch @switch && newValue is { Model.SupportsEthernet: true } portItem)
        {
            AccessVlanInput = @switch.GetPortVlanConfiguration(portItem.Model).AccessVlan.Value.ToString();
        }
        else
        {
            AccessVlanInput = string.Empty;
        }

        OnPropertyChanged(nameof(HasSelectedInterface));
        OnPropertyChanged(nameof(SelectedInterfaceIsSwitchPort));
        MakeAccessPortCommand.NotifyCanExecuteChanged();
        MakeTrunkPortCommand.NotifyCanExecuteChanged();
        SetAccessVlanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedInterfaceIPv4))]
    private void ApplyIPv4()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        if (!IPv4Address.TryParse(IPv4AddressInput.Trim(), out var address))
        {
            IPv4ValidationError = "Enter a valid IPv4 address, e.g. 192.168.1.10.";
            return;
        }

        if (!TryResolvePrefixLength(IPv4SubnetMaskInput, out var prefixLength))
        {
            IPv4ValidationError = "Enter a subnet mask (255.255.255.0) or a CIDR prefix (24 or /24).";
            return;
        }

        // The service owns the domain validation, the dirty flag and the InterfacesChanged
        // notification (which loops back into Refresh() and re-seeds the editor from the saved value).
        var result = _deviceService.SetInterfaceIPv4Configuration(networkInterface, address, prefixLength);
        IPv4ValidationError = result.IsSuccess ? null : result.ErrorMessage;
    }

    [RelayCommand(CanExecute = nameof(CanClearSelectedInterfaceIPv4))]
    private void ClearIPv4()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        _deviceService.ClearInterfaceIPv4Configuration(networkInterface);
        IPv4ValidationError = null;
    }

    private bool CanEditSelectedInterfaceIPv4() => SelectedInterface is not null;

    private bool CanClearSelectedInterfaceIPv4() => SelectedInterface is { HasIPv4: true };

    [RelayCommand(CanExecute = nameof(CanEditSelectedInterfaceIPv4))]
    private void ApplyDefaultGateway()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        if (!IPv4Address.TryParse(DefaultGatewayInput.Trim(), out var gateway))
        {
            DefaultGatewayValidationError = "Enter a valid IPv4 address, e.g. 192.168.1.1.";
            return;
        }

        // The service owns the domain validation, the dirty flag and the InterfacesChanged
        // notification (which loops back into Refresh() and re-seeds the editor from the saved value).
        var result = _deviceService.SetInterfaceIPv4DefaultGateway(networkInterface, gateway);
        DefaultGatewayValidationError = result.IsSuccess ? null : result.ErrorMessage;
    }

    [RelayCommand(CanExecute = nameof(CanClearSelectedInterfaceDefaultGateway))]
    private void ClearDefaultGateway()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        _deviceService.ClearInterfaceIPv4DefaultGateway(networkInterface);
        DefaultGatewayValidationError = null;
    }

    private bool CanClearSelectedInterfaceDefaultGateway() => SelectedInterface is { HasDefaultGateway: true };

    [RelayCommand(CanExecute = nameof(CanEditSelectedInterfaceIPv6))]
    private void AddIPv6()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        if (!IPv6Address.TryParse(IPv6AddressInput.Trim(), out var address))
        {
            IPv6ValidationError = "Enter a valid IPv6 address, e.g. 2001:db8:1::10.";
            return;
        }

        if (!TryResolveIPv6PrefixLength(IPv6PrefixLengthInput, out var prefixLength))
        {
            IPv6ValidationError = "Enter an IPv6 prefix length between 0 and 128 (e.g. 64 or /64).";
            return;
        }

        // The service owns the domain validation, the duplicate-address rule, the dirty flag and
        // the InterfacesChanged notification (which loops back into Refresh()).
        var result = _deviceService.AddInterfaceIPv6Configuration(networkInterface, address, prefixLength);
        if (result.IsSuccess)
        {
            IPv6ValidationError = null;
            IPv6AddressInput = string.Empty;
            IPv6PrefixLengthInput = string.Empty;
        }
        else
        {
            IPv6ValidationError = result.ErrorMessage;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearSelectedInterfaceIPv6))]
    private void ClearIPv6()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        _deviceService.ClearInterfaceIPv6Configuration(networkInterface);
        IPv6ValidationError = null;
        IPv6AddressInput = string.Empty;
        IPv6PrefixLengthInput = string.Empty;
    }

    private bool CanEditSelectedInterfaceIPv6() => SelectedInterface is not null;

    private bool CanClearSelectedInterfaceIPv6() => SelectedInterface is { HasIPv6: true };

    [RelayCommand(CanExecute = nameof(CanClearArp))]
    private void ClearArp()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        // Runtime state only - the service clears the cache and raises InterfacesChanged (which
        // loops back into Refresh() and rebuilds the ARP table display); it does not touch the
        // project's dirty flag.
        _deviceService.ClearInterfaceArpCache(networkInterface);
    }

    private bool CanClearArp() => SelectedInterface is { HasArpEntries: true };

    [RelayCommand(CanExecute = nameof(CanPing))]
    private async Task Ping()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        // The dialog is a self-contained tool (it drives its own Ping calls) - this just opens it
        // and awaits its close, exactly like every other dialog opened through IDialogService.
        await _dialogService.ShowPingAsync(networkInterface);
    }

    private bool CanPing() => SelectedInterface is { HasIPv4: true };

    [RelayCommand(CanExecute = nameof(CanDnsLookup))]
    private async Task DnsLookup()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        // Same self-contained-tool shape as Ping (brief section 40, "DNS lookup tool") - it drives
        // its own IDnsResolver calls; this just opens it and awaits its close.
        await _dialogService.ShowDnsLookupAsync(networkInterface);
    }

    private bool CanDnsLookup() => SelectedInterface is { HasIPv4: true };

    [RelayCommand(CanExecute = nameof(CanUseDhcp))]
    private async Task Dhcp()
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        // Same self-contained-tool shape as Ping / DNS Lookup (brief section 48) - the dialog drives
        // its own IDhcpClient calls; this just opens it and awaits its close.
        await _dialogService.ShowDhcpAsync(networkInterface);
    }

    // DHCP needs a link-layer identity, so it is offered only for an Ethernet interface (one with a MAC).
    private bool CanUseDhcp() => SelectedInterface is { HasMacAddress: true };

    [RelayCommand(CanExecute = nameof(CanApplyDnsServer))]
    private void ApplyDnsServer()
    {
        if (_selectedDevice is null)
        {
            return;
        }

        if (!IPv4Address.TryParse(DnsServerAddressInput.Trim(), out var address))
        {
            DnsServerValidationError = "Enter a valid IPv4 address, e.g. 192.168.1.53.";
            return;
        }

        // Manual/programmatic configuration only in this phase (brief section 20/59) - DHCP-provided
        // DNS server configuration is Phase 24. A device's configured server list is a plain
        // per-device table (IDnsClientConfigurationStore), not part of the persisted device model
        // yet, so this does not mark the project dirty.
        _dnsConfiguration.SetServers(_selectedDevice, [address]);
        DnsServerValidationError = null;
    }

    private bool CanApplyDnsServer() => _selectedDevice is not null;

    [RelayCommand(CanExecute = nameof(CanClearDnsServer))]
    private void ClearDnsServer()
    {
        if (_selectedDevice is null)
        {
            return;
        }

        _dnsConfiguration.ClearServers(_selectedDevice);
        DnsServerAddressInput = string.Empty;
        DnsServerValidationError = null;
    }

    private bool CanClearDnsServer() => _selectedDevice is not null && DnsServerAddressInput.Length > 0;

    // Accepts a bare prefix length (64) or a slash prefix (/64).
    private static bool TryResolveIPv6PrefixLength(string? input, out int prefixLength)
    {
        prefixLength = 0;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        if (int.TryParse(text, out var parsed) && parsed is >= 0 and <= 128)
        {
            prefixLength = parsed;
            return true;
        }

        return false;
    }

    // Accepts a dotted-decimal mask (255.255.255.0), a bare prefix (24) or a slash prefix (/24).
    private static bool TryResolvePrefixLength(string? input, out int prefixLength)
    {
        prefixLength = 0;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        if (int.TryParse(text, out var parsedPrefix) && parsedPrefix is >= 0 and <= 32)
        {
            prefixLength = parsedPrefix;
            return true;
        }

        if (SubnetMask.TryParse(text, out var mask))
        {
            prefixLength = mask.PrefixLength;
            return true;
        }

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanEnableInterface))]
    private void EnableInterface() => SetSelectedInterfaceAdministrativeState(InterfaceAdministrativeState.Enabled);

    private bool CanEnableInterface() => SelectedInterface is { IsEnabled: false };

    [RelayCommand(CanExecute = nameof(CanDisableInterface))]
    private void DisableInterface() => SetSelectedInterfaceAdministrativeState(InterfaceAdministrativeState.Disabled);

    private bool CanDisableInterface() => SelectedInterface is { IsEnabled: true };

    private void SetSelectedInterfaceAdministrativeState(InterfaceAdministrativeState state)
    {
        if (SelectedInterface?.Model is not { } networkInterface)
        {
            return;
        }

        // Goes through IDeviceService so the domain rule (disabling forces the link down), the
        // dirty flag and the InterfacesChanged notification are all handled in one place; the
        // notification loops back into Refresh(), which rebuilds the list and re-selects by name.
        _deviceService.SetInterfaceAdministrativeState(networkInterface, state);
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (_selectedDevice is null)
        {
            return;
        }

        var trimmedName = DeviceName.Trim();
        var error = ValidateName(trimmedName);
        if (error is not null)
        {
            ValidationError = error;
            return;
        }

        // Blank/too-long is already ruled out above; a NotFound failure here would mean the
        // device left the current network between selection and Apply (e.g. project switched) -
        // surfaced the same friendly way rather than throwing.
        var result = _deviceService.RenameDevice(_selectedDevice, trimmedName);
        if (!result.IsSuccess)
        {
            ValidationError = result.ErrorMessage;
            return;
        }

        // Keeps the canvas label in sync with the rename (see CanvasItem.Rename) and marks the
        // project dirty through the same door every other edit in the app uses - see
        // docs/architecture/project-management.md.
        _itemsState.RenameItem(_selectedDevice.Id, trimmedName);
        _applicationState.MarkCurrentProjectDirty();

        _originalName = trimmedName;
        DeviceName = trimmedName;
        UpdateEditingState();
    }

    private bool CanApply() => IsDirty && ValidationError is null;

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Cancel()
    {
        DeviceName = _originalName;
        ValidationError = null;
    }

    [RelayCommand]
    private void PowerOn() => SetPowerState(poweredOn: true);

    [RelayCommand]
    private void PowerOff() => SetPowerState(poweredOn: false);

    private void SetPowerState(bool poweredOn)
    {
        if (_selectedDevice is null || IsPoweredOn == poweredOn)
        {
            return;
        }

        if (poweredOn)
        {
            _selectedDevice.PowerOn();
        }
        else
        {
            _selectedDevice.PowerOff();
        }

        _applicationState.MarkCurrentProjectDirty();
        IsPoweredOn = poweredOn;
        DeviceStateDisplayName = DisplayNameFor(_selectedDevice.OperationalState);
    }

    private void Refresh()
    {
        var ids = _canvasSelectionState.SelectedIds;

        if (ids.Count == 0)
        {
            SetSelectedDevice(null);
            SelectionMode = DevicePropertiesSelectionMode.None;
            return;
        }

        if (ids.Count > 1)
        {
            SetSelectedDevice(null);
            SelectionMode = DevicePropertiesSelectionMode.Multiple;
            return;
        }

        // Canvas selection is id-based and knows nothing about NetworkDevice itself (see
        // ICanvasSelectionState) - the matching device, if any, is looked up from the current
        // network's own device list, the single source of truth for "what devices exist".
        var device = _applicationState.CurrentNetwork?.Devices.FirstOrDefault(d => d.Id == ids.Single());
        SetSelectedDevice(device);
        SelectionMode = device is null ? DevicePropertiesSelectionMode.None : DevicePropertiesSelectionMode.Single;
    }

    private void SetSelectedDevice(NetworkDevice? device)
    {
        // Remember which interface was selected so a rebuild (e.g. after Enable/Disable or a
        // connection change) does not silently drop the user's interface selection.
        var previousInterfaceName = _selectedInterfaceName;
        var sameDevice = ReferenceEquals(device, _selectedDevice);

        _selectedDevice = device;
        SelectedInterface = null;
        Interfaces.Clear();

        if (device is null)
        {
            _originalName = string.Empty;
            DeviceName = string.Empty;
            DeviceTypeDisplayName = string.Empty;
            DeviceStateDisplayName = string.Empty;
            DeviceIdText = string.Empty;
            IsPoweredOn = false;
            DeviceIcon = null;
            _selectedInterfaceName = null;
            DnsServerAddressInput = string.Empty;
            DnsServerValidationError = null;
        }
        else
        {
            _originalName = device.Name;
            DeviceName = device.Name;
            DeviceTypeDisplayName = DeviceTypeRegistry.Get(device.DeviceType).DisplayName;
            DeviceStateDisplayName = DisplayNameFor(device.OperationalState);
            DeviceIdText = device.Id.ToString();
            IsPoweredOn = device.OperationalState == DeviceOperationalState.PoweredOn;
            DeviceIcon = ResolveIcon(device.DeviceType);
            DnsServerAddressInput = _dnsConfiguration.GetServers(device).Select(a => a.ToString()).FirstOrDefault() ?? string.Empty;
            DnsServerValidationError = null;

            foreach (var networkInterface in device.Interfaces)
            {
                Interfaces.Add(new DeviceInterfaceItem(networkInterface, device as Core.Devices.Switch));
            }

            // Re-select the same interface after a same-device rebuild.
            if (sameDevice && previousInterfaceName is not null)
            {
                SelectedInterface = Interfaces.FirstOrDefault(i => i.Name == previousInterfaceName);
            }
        }

        LoadMacAddressTable();
        LoadVlanTable();
        LoadRoutingTable();
        LoadStaticRoutes();

        OnPropertyChanged(nameof(HasInterfaces));
        OnPropertyChanged(nameof(IsSwitch));
        OnPropertyChanged(nameof(IsRouter));
        OnPropertyChanged(nameof(SelectedInterfaceIsSwitchPort));
        ValidationError = null;
        NewVlanIdInput = string.Empty;
        NewVlanNameInput = string.Empty;
        VlanValidationError = null;
        if (!sameDevice)
        {
            ResetStaticRouteForm();
        }

        UpdateEditingState();
        ApplyDnsServerCommand.NotifyCanExecuteChanged();
        ClearDnsServerCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Re-reads the selected switch's MAC address table (Phase 25). No-op for a non-switch device.</summary>
    [RelayCommand]
    private void RefreshMacTable() => LoadMacAddressTable();

    private void LoadMacAddressTable()
    {
        MacAddressTable.Clear();

        if (_selectedDevice is Core.Devices.Switch @switch)
        {
            var now = _simulationClock.Now;
            foreach (var entry in @switch.MacAddressTable.GetEntries())
            {
                MacAddressTable.Add(new SwitchMacEntryRow(entry, now));
            }
        }

        OnPropertyChanged(nameof(HasMacAddressTable));
    }

    // ---- Routing table (Phase 27) + static routes (Phase 28) -----------------------------------

    /// <summary>Re-reads the selected router's routing table and static routes. No-op for a non-router device.</summary>
    [RelayCommand]
    private void RefreshRoutingTable()
    {
        LoadRoutingTable();
        LoadStaticRoutes();
    }

    private void LoadRoutingTable()
    {
        RoutingTable.Clear();

        if (_selectedDevice is Core.Devices.Router router)
        {
            // Keep the derived connected routes in step with the current interface addressing
            // before projecting them (the routing engine does the same before every decision).
            router.SyncConnectedRoutes();
            var allRoutes = router.RoutingTable.GetRoutes();
            foreach (var route in allRoutes)
            {
                var resolution = Core.Routing.RouteResolver.Resolve(allRoutes, route, route.Destination.NetworkAddress);
                RoutingTable.Add(new RouterRouteRow(route, resolution));
            }
        }

        OnPropertyChanged(nameof(HasRoutingTable));
    }

    private void LoadStaticRoutes()
    {
        StaticRoutes.Clear();

        if (_selectedDevice is Core.Devices.Router router)
        {
            foreach (var view in _staticRouteService.GetStaticRoutes(router))
            {
                StaticRoutes.Add(new StaticRouteRow(view));
            }
        }

        OnPropertyChanged(nameof(HasStaticRoutes));
    }

    [RelayCommand]
    private void AddOrUpdateStaticRoute()
    {
        if (_selectedDevice is not Core.Devices.Router router)
        {
            return;
        }

        var prefix = ParsePrefixInput(StaticRoutePrefixInput);
        var input = new StaticRouteInput
        {
            DestinationNetwork = StaticRouteDestinationInput.Trim(),
            PrefixLength = prefix,
            NextHop = StaticRouteNextHopInput,
            OutgoingInterfaceName = StaticRouteInterfaceInput,
        };

        var result = EditingStaticRouteId is { } editingId
            ? _staticRouteService.UpdateStaticRoute(router, editingId, input)
            : _staticRouteService.AddStaticRoute(router, input);

        if (result.IsSuccess)
        {
            ResetStaticRouteForm();
        }
        else
        {
            StaticRouteValidationError = result.ErrorMessage;
        }
    }

    [RelayCommand]
    private void EditStaticRoute(StaticRouteRow? row)
    {
        if (row is null)
        {
            return;
        }

        StaticRouteDestinationInput = row.DestinationNetwork;
        StaticRoutePrefixInput = row.PrefixLength.ToString();
        StaticRouteNextHopInput = row.NextHop ?? string.Empty;
        StaticRouteInterfaceInput = row.OutgoingInterfaceName ?? string.Empty;
        StaticRouteValidationError = null;
        EditingStaticRouteId = row.Id;
    }

    [RelayCommand]
    private void RemoveStaticRoute(StaticRouteRow? row)
    {
        if (row is null || _selectedDevice is not Core.Devices.Router router)
        {
            return;
        }

        var result = _staticRouteService.RemoveStaticRoute(router, row.Id);
        if (!result.IsSuccess)
        {
            StaticRouteValidationError = result.ErrorMessage;
            return;
        }

        if (EditingStaticRouteId == row.Id)
        {
            ResetStaticRouteForm();
        }
    }

    [RelayCommand(CanExecute = nameof(IsEditingStaticRoute))]
    private void CancelStaticRouteEdit() => ResetStaticRouteForm();

    /// <summary>Fills the static-route form with the default route template (<c>0.0.0.0/0</c>).</summary>
    [RelayCommand]
    private void UseDefaultRouteTemplate()
    {
        StaticRouteDestinationInput = "0.0.0.0";
        StaticRoutePrefixInput = "0";
        StaticRouteValidationError = null;
    }

    private void ResetStaticRouteForm()
    {
        StaticRouteDestinationInput = string.Empty;
        StaticRoutePrefixInput = string.Empty;
        StaticRouteNextHopInput = string.Empty;
        StaticRouteInterfaceInput = string.Empty;
        StaticRouteValidationError = null;
        EditingStaticRouteId = null;
    }

    // Accepts a bare prefix length (24) or a slash prefix (/24); returns -1 when unparseable so the
    // service surfaces the "prefix must be 0-32" message.
    private static int ParsePrefixInput(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        return int.TryParse(text, out var parsed) ? parsed : -1;
    }

    // ---- VLAN configuration (Phase 26) -------------------------------------------------------

    private void LoadVlanTable()
    {
        Vlans.Clear();

        if (_selectedDevice is Core.Devices.Switch @switch)
        {
            foreach (var vlan in @switch.Vlans.GetAllVlans())
            {
                Vlans.Add(new SwitchVlanRow(vlan, @switch));
            }
        }

        OnPropertyChanged(nameof(HasVlans));
    }

    [RelayCommand(CanExecute = nameof(CanCreateVlan))]
    private void CreateVlan()
    {
        if (_selectedDevice is not Core.Devices.Switch @switch)
        {
            return;
        }

        if (!int.TryParse(NewVlanIdInput.Trim(), out var id) || !VlanId.IsValid(id))
        {
            VlanValidationError = $"Enter a VLAN id between {VlanId.MinValue} and {VlanId.MaxValue}.";
            return;
        }

        try
        {
            @switch.CreateVlan(new VlanId(id), string.IsNullOrWhiteSpace(NewVlanNameInput) ? null : NewVlanNameInput.Trim());
        }
        catch (Core.Common.Exceptions.DomainException ex)
        {
            VlanValidationError = ex.Message;
            return;
        }

        _applicationState.MarkCurrentProjectDirty();
        NewVlanIdInput = string.Empty;
        NewVlanNameInput = string.Empty;
        VlanValidationError = null;
        LoadVlanTable();
    }

    private bool CanCreateVlan() => _selectedDevice is Core.Devices.Switch;

    [RelayCommand]
    private void RemoveVlan(SwitchVlanRow? row)
    {
        if (row is null || _selectedDevice is not Core.Devices.Switch @switch)
        {
            return;
        }

        try
        {
            @switch.RemoveVlan(new VlanId(row.Id));
        }
        catch (Core.Common.Exceptions.DomainException ex)
        {
            VlanValidationError = ex.Message;
            return;
        }

        _applicationState.MarkCurrentProjectDirty();
        VlanValidationError = null;
        LoadVlanTable();
        LoadMacAddressTable();
    }

    [RelayCommand(CanExecute = nameof(CanConfigureSwitchPort))]
    private void MakeAccessPort()
    {
        if (SelectedInterface?.Model is not { } port || _selectedDevice is not Core.Devices.Switch @switch)
        {
            return;
        }

        try
        {
            @switch.SetPortMode(port, SwitchPortMode.Access);
        }
        catch (Core.Common.Exceptions.DomainException ex)
        {
            PortVlanValidationError = ex.Message;
            return;
        }

        ApplyPortVlanChange(port, @switch);
    }

    [RelayCommand(CanExecute = nameof(CanConfigureSwitchPort))]
    private void MakeTrunkPort()
    {
        if (SelectedInterface?.Model is not { } port || _selectedDevice is not Core.Devices.Switch @switch)
        {
            return;
        }

        try
        {
            @switch.SetPortMode(port, SwitchPortMode.Trunk);
        }
        catch (Core.Common.Exceptions.DomainException ex)
        {
            PortVlanValidationError = ex.Message;
            return;
        }

        ApplyPortVlanChange(port, @switch);
    }

    [RelayCommand(CanExecute = nameof(CanConfigureSwitchPort))]
    private void SetAccessVlan()
    {
        if (SelectedInterface?.Model is not { } port || _selectedDevice is not Core.Devices.Switch @switch)
        {
            return;
        }

        if (!int.TryParse(AccessVlanInput.Trim(), out var id) || !VlanId.IsValid(id))
        {
            PortVlanValidationError = $"Enter a VLAN id between {VlanId.MinValue} and {VlanId.MaxValue}.";
            return;
        }

        try
        {
            @switch.ConfigureAccessPort(port, new VlanId(id));
        }
        catch (Core.Common.Exceptions.DomainException ex)
        {
            PortVlanValidationError = ex.Message;
            return;
        }

        ApplyPortVlanChange(port, @switch);
    }

    private bool CanConfigureSwitchPort() => SelectedInterfaceIsSwitchPort;

    private void ApplyPortVlanChange(NetworkInterface port, Core.Devices.Switch @switch)
    {
        _applicationState.MarkCurrentProjectDirty();
        PortVlanValidationError = null;
        AccessVlanInput = @switch.GetPortVlanConfiguration(port).AccessVlan.Value.ToString();
        LoadVlanTable();
        LoadMacAddressTable();
        RebuildInterfaceList();
    }

    private void RebuildInterfaceList()
    {
        if (_selectedDevice is null)
        {
            return;
        }

        var selectedName = _selectedInterfaceName;
        Interfaces.Clear();
        foreach (var networkInterface in _selectedDevice.Interfaces)
        {
            Interfaces.Add(new DeviceInterfaceItem(networkInterface, _selectedDevice as Core.Devices.Switch));
        }

        SelectedInterface = Interfaces.FirstOrDefault(i => i.Name == selectedName);
    }

    private void UpdateEditingState()
    {
        IsDirty = _selectedDevice is not null && DeviceName != _originalName;
        ValidationError = IsDirty ? ValidateName(DeviceName.Trim()) : null;

        ApplyCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Device name cannot be empty.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"Device name cannot exceed {MaxNameLength} characters.";
        }

        return null;
    }

    private static string DisplayNameFor(DeviceOperationalState state) => state switch
    {
        DeviceOperationalState.PoweredOn => "Running",
        DeviceOperationalState.PoweredOff => "Powered Off",
        _ => state.ToString(),
    };

    // Same resolution pattern as DeviceCatalog/NetworkCanvasControl: Icon.* resources (Theme/
    // Icons.axaml) are plain, theme-invariant StreamGeometry, so a one-time lookup per selection
    // change is safe (no Dark/Light re-resolution needed). Avalonia.Application.Current is null in
    // headless unit tests, so this degrades to a null icon there - callers must treat it as optional.
    private static Geometry? ResolveIcon(DeviceType deviceType) =>
        global::Avalonia.Application.Current?.TryFindResource(DeviceIcons.ResourceKeyFor(deviceType), out var resource) == true
            ? resource as Geometry
            : null;
}

/// <summary>Which of the Properties panel's three states is showing - see the Phase 12 brief, "No
/// Device Selected"/"Selected Device"/"Multiple Selection".</summary>
public enum DevicePropertiesSelectionMode
{
    None,
    Single,
    Multiple,
}

/// <summary>
/// Observable projection of one <see cref="NetworkInterface"/> for the Properties panel's
/// interface list: name/type/speed plus administrative + operational + connection state, the
/// Ethernet MAC address (Phase 17, read-only), and the IPv4 configuration (Phase 18 - shown here,
/// edited through the parent ViewModel's <see cref="DevicePropertiesViewModel.ApplyIPv4Command"/>).
/// The <see cref="IsSelected"/> flag the View binds to for highlighting.
/// </summary>
public partial class DeviceInterfaceItem : ObservableObject
{
    public DeviceInterfaceItem(NetworkInterface networkInterface, Core.Devices.Switch? owningSwitch = null)
    {
        Model = networkInterface;
        Name = networkInterface.Name;

        // Phase 26: when this interface is a port on a switch, project its VLAN configuration for
        // display (mode, access VLAN, or trunk native + allowed VLANs).
        if (owningSwitch is not null && networkInterface.SupportsEthernet)
        {
            var config = owningSwitch.GetPortVlanConfiguration(networkInterface);
            IsSwitchPort = true;
            SwitchPortModeText = config.Mode.ToString();
            AccessVlanText = config.AccessVlan.Value.ToString();
            NativeVlanText = config.NativeVlan.Value.ToString();
            TrunkAllowedVlansText = config.AllowsAllVlans
                ? "all"
                : config.AllowedVlans.Count == 0 ? "none" : string.Join(", ", config.AllowedVlans.Select(v => v.Value));
            VlanSummaryText = config.IsAccess
                ? $"Access ֲ· VLAN {config.AccessVlan}"
                : $"Trunk ֲ· native {config.NativeVlan} ֲ· allowed {TrunkAllowedVlansText}";
        }
        ShortName = networkInterface.ShortName;
        InterfaceTypeText = InterfaceTypeInfo.DisplayName(networkInterface.InterfaceType);
        SpeedText = networkInterface.Speed.ToDisplayString();
        IsUp = networkInterface.OperationalState == InterfaceOperationalState.Up;
        IsEnabled = networkInterface.IsEnabled;
        IsConnected = networkInterface.IsConnected;
        Description = networkInterface.Description;
        MacAddressText = networkInterface.MacAddress?.ToString();

        var primaryIPv4 = networkInterface.PrimaryIPv4Configuration;
        IPv4AddressText = primaryIPv4?.Address.ToString();
        SubnetMaskText = primaryIPv4?.SubnetMask.ToString();
        CidrText = primaryIPv4?.Cidr;
        SecondaryIPv4Count = networkInterface.IPv4Configurations.Count(c => !c.IsPrimary);
        DefaultGatewayText = networkInterface.IPv4DefaultGateway?.ToString();

        // IPv6: an interface commonly carries more than one address, so the whole list is exposed
        // for display (primary first) alongside the primary-only convenience fields.
        IPv6Cidrs = networkInterface.IPv6Configurations
            .OrderByDescending(c => c.IsPrimary)
            .Select(c => c.Cidr)
            .ToArray();
        IPv6CidrText = networkInterface.PrimaryIPv6Configuration?.Cidr;
        SecondaryIPv6Count = networkInterface.IPv6Configurations.Count(c => !c.IsPrimary);

        // ARP cache (Phase 20) - a read-only snapshot for the diagnostics view. Runtime state,
        // usually empty in the editor (a running simulation populates it in a later phase).
        ArpEntries = networkInterface.ArpCache.Entries
            .OrderBy(e => e.ProtocolAddress)
            .Select(e => new ArpEntryRow(e))
            .ToArray();
    }

    /// <summary>The live domain interface this item projects. Used by the ViewModel's Enable/Disable commands.</summary>
    public NetworkInterface Model { get; }

    public string Name { get; }

    public string ShortName { get; }

    public string InterfaceTypeText { get; }

    public string SpeedText { get; }

    public bool IsUp { get; }

    public bool IsEnabled { get; }

    public bool IsConnected { get; }

    public string? Description { get; }

    /// <summary>Canonical MAC address for an Ethernet interface, or null for a non-Ethernet one (Serial/Console).</summary>
    public string? MacAddressText { get; }

    /// <summary>True when this interface has a MAC address to show.</summary>
    public bool HasMacAddress => MacAddressText is not null;

    /// <summary>The primary IPv4 address in dotted-decimal form, or null when no IPv4 address is configured.</summary>
    public string? IPv4AddressText { get; }

    /// <summary>The primary IPv4 address's subnet mask in dotted-decimal form, or null when none is configured.</summary>
    public string? SubnetMaskText { get; }

    /// <summary>The primary IPv4 address in CIDR form (e.g. 192.168.1.10/24), or null when none is configured.</summary>
    public string? CidrText { get; }

    /// <summary>How many secondary IPv4 addresses (beyond the primary) are configured on this interface.</summary>
    public int SecondaryIPv4Count { get; }

    /// <summary>True when this interface has an IPv4 address to show.</summary>
    public bool HasIPv4 => IPv4AddressText is not null;

    /// <summary>The interface's IPv4 default gateway in dotted-decimal form (Phase 27), or null when none is configured.</summary>
    public string? DefaultGatewayText { get; }

    /// <summary>True when this interface has an IPv4 default gateway configured.</summary>
    public bool HasDefaultGateway => DefaultGatewayText is not null;

    /// <summary>True when this interface carries at least one secondary IPv4 address.</summary>
    public bool HasSecondaryIPv4 => SecondaryIPv4Count > 0;

    /// <summary>Every configured IPv6 address in CIDR form (primary first), for the multi-address display.</summary>
    public IReadOnlyList<string> IPv6Cidrs { get; } = [];

    /// <summary>The primary IPv6 address in CIDR form (e.g. 2001:db8:1::1/64), or null when no IPv6 address is configured.</summary>
    public string? IPv6CidrText { get; }

    /// <summary>How many secondary IPv6 addresses (beyond the primary) are configured on this interface.</summary>
    public int SecondaryIPv6Count { get; }

    /// <summary>True when this interface has at least one IPv6 address to show.</summary>
    public bool HasIPv6 => IPv6CidrText is not null;

    /// <summary>True when this interface carries at least one secondary IPv6 address.</summary>
    public bool HasSecondaryIPv6 => SecondaryIPv6Count > 0;

    /// <summary>The interface's ARP cache as read-only rows (IPv4 address ascending). Runtime state - usually empty in the editor.</summary>
    public IReadOnlyList<ArpEntryRow> ArpEntries { get; } = [];

    /// <summary>How many live ARP entries this interface currently holds.</summary>
    public int ArpEntryCount => ArpEntries.Count;

    /// <summary>True when this interface has at least one ARP entry to show.</summary>
    public bool HasArpEntries => ArpEntries.Count > 0;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Administrative state text - "Enabled" / "Disabled".</summary>
    public string AdminStateText => IsEnabled ? "Enabled" : "Disabled";

    /// <summary>Operational status text - a disabled interface reads "Disabled" regardless of link state.</summary>
    public string StatusText => !IsEnabled ? "Disabled" : IsUp ? "Up" : "Down";

    public string ConnectionText => IsConnected ? "Connected" : "Not connected";

    /// <summary>True when the interface can accept a new connection (enabled and not already cabled).</summary>
    public bool IsAvailable => IsEnabled && !IsConnected;

    /// <summary>Enabled but the link is not up - the "neutral" status-dot case (disabled and up are handled separately).</summary>
    public bool IsEnabledAndDown => IsEnabled && !IsUp;

    // ---- Switch port VLAN configuration (Phase 26), populated only when this is a port on a switch ----

    /// <summary>True when this interface is an Ethernet port on a switch (its VLAN fields are populated).</summary>
    public bool IsSwitchPort { get; }

    /// <summary>"Access" or "Trunk". Empty for a non-switch-port.</summary>
    public string SwitchPortModeText { get; } = string.Empty;

    /// <summary>The access VLAN id as text (meaningful when the port is in access mode).</summary>
    public string AccessVlanText { get; } = string.Empty;

    /// <summary>The trunk native VLAN id as text (meaningful when the port is a trunk).</summary>
    public string NativeVlanText { get; } = string.Empty;

    /// <summary>The trunk allowed VLANs - "all", "none" or a comma-separated list.</summary>
    public string TrunkAllowedVlansText { get; } = string.Empty;

    /// <summary>One-line VLAN summary for the interface list (e.g. "Access ֲ· VLAN 10").</summary>
    public string VlanSummaryText { get; } = string.Empty;
}

/// <summary>
/// One row of a switch's VLAN database (Phase 26) for the Properties panel: the VLAN id and name,
/// which ports are in it, whether it is active, and whether the UI may offer to remove it (the
/// default VLAN and any VLAN still referenced by a port cannot be removed). A projection of
/// <see cref="NetSim.Core.Vlans.Vlan"/> - the VLAN domain stays out of the UI.
/// </summary>
public sealed class SwitchVlanRow
{
    public SwitchVlanRow(NetSim.Core.Vlans.Vlan vlan, Core.Devices.Switch owningSwitch)
    {
        Id = vlan.Id.Value;
        IdText = vlan.Id.Value.ToString();
        Name = vlan.Name;
        IsActive = vlan.IsActive;
        IsDefault = vlan.IsDefault;

        var ports = owningSwitch.PortVlanConfigurations
            .Where(p => p.Configuration.CarriesVlan(vlan.Id))
            .Select(p => p.Port.ShortName)
            .ToList();
        PortsText = ports.Count == 0 ? "-" : string.Join(", ", ports);

        CanRemove = !vlan.IsDefault && !owningSwitch.IsVlanInUse(vlan.Id);
    }

    public int Id { get; }

    public string IdText { get; }

    public string Name { get; }

    public string PortsText { get; }

    public bool IsActive { get; }

    public bool IsDefault { get; }

    public bool CanRemove { get; }
}

/// <summary>
/// One read-only row of a router's IPv4 routing table (Phase 27) for the Properties panel: the
/// destination network + prefix, the next hop ("Direct" for a connected route), the outgoing
/// interface, the route type and whether it is currently Active (its interface is up). A projection
/// of <see cref="NetSim.Core.Routing.Route"/> - the routing engine stays out of the UI.
/// </summary>
public sealed class RouterRouteRow
{
    public RouterRouteRow(
        NetSim.Core.Routing.Route route,
        NetSim.Core.Routing.RouteResolution? resolution = null)
    {
        NetworkText = route.Destination.NetworkAddress.ToString();
        PrefixText = $"/{route.Destination.PrefixLength}";
        NextHopText = route.NextHop?.ToString() ?? "Direct";

        // Show the resolved outgoing interface for a recursive static route (which carries no
        // interface of its own); fall back to the route's own interface, then "-".
        InterfaceText = resolution?.OutgoingInterface.ShortName
            ?? route.OutgoingInterface?.ShortName
            ?? "-";
        TypeText = route.Type.ToString();
        CodeText = route.Type.Code();

        // A connected route's usability is its interface state; a static route is usable only when
        // its (possibly recursive) next hop currently resolves.
        IsActive = route.Type == NetSim.Core.Routing.RouteType.Connected
            ? route.IsActive
            : resolution is not null;
        StatusText = IsActive ? "Active" : "Inactive";
    }

    public string NetworkText { get; }

    public string PrefixText { get; }

    public string NextHopText { get; }

    public string InterfaceText { get; }

    public string TypeText { get; }

    /// <summary>Single-letter route-table code (C, S, ...), mirroring a router's routing-table legend.</summary>
    public string CodeText { get; }

    public bool IsActive { get; }

    public string StatusText { get; }
}

/// <summary>
/// One editable row of a router's static-route configuration (Phase 28) for the Properties panel:
/// its destination, next hop / exit interface, metric, administrative distance, route type and
/// current status. Carries the raw values needed to seed the edit form. A projection of
/// <see cref="StaticRouteView"/> - the routing engine and the static-route service stay out of the View.
/// </summary>
public sealed class StaticRouteRow
{
    public StaticRouteRow(StaticRouteView view)
    {
        Id = view.Id;
        DestinationNetwork = view.DestinationNetwork;
        PrefixLength = view.PrefixLength;
        DestinationText = view.DestinationCidr;
        NextHop = view.NextHop;
        NextHopText = view.NextHop ?? "-";
        OutgoingInterfaceName = view.OutgoingInterfaceName;
        InterfaceText = view.OutgoingInterfaceName ?? "-";
        TypeText = view.RouteTypeText;
        MetricText = view.Metric.ToString();
        AdministrativeDistanceText = view.AdministrativeDistance.ToString();
        StatusText = view.StatusText;
        IsActive = view.IsActive;
        IsDefaultRoute = view.IsDefaultRoute;
    }

    public Guid Id { get; }

    /// <summary>Raw destination network address, for seeding the edit form.</summary>
    public string DestinationNetwork { get; }

    /// <summary>Raw prefix length, for seeding the edit form.</summary>
    public int PrefixLength { get; }

    /// <summary>Raw next hop (may be null), for seeding the edit form.</summary>
    public string? NextHop { get; }

    /// <summary>Raw outgoing interface short name (may be null), for seeding the edit form.</summary>
    public string? OutgoingInterfaceName { get; }

    public string DestinationText { get; }

    public string NextHopText { get; }

    public string InterfaceText { get; }

    public string TypeText { get; }

    public string MetricText { get; }

    public string AdministrativeDistanceText { get; }

    public string StatusText { get; }

    public bool IsActive { get; }

    public bool IsDefaultRoute { get; }
}

/// <summary>
/// One read-only row of a <see cref="DeviceInterfaceItem"/>'s ARP table (Phase 20): the IPv4
/// address, the MAC it resolves to, and whether the entry is Dynamic or Static. A projection of
/// <see cref="NetSim.Core.Networking.ArpCacheEntry"/> for the diagnostics view - the ARP
/// domain stays out of the UI.
/// </summary>
public sealed class ArpEntryRow
{
    public ArpEntryRow(NetSim.Core.Networking.ArpCacheEntry entry)
    {
        IpText = entry.ProtocolAddress.ToString();
        MacText = entry.HardwareAddress.ToString();
        TypeText = entry.State.ToString();
    }

    public string IpText { get; }

    public string MacText { get; }

    public string TypeText { get; }
}

/// <summary>
/// One read-only row of a switch's MAC address forwarding table (Phase 25): the learned MAC
/// address, the port it was learned on, how old the entry is (in whole seconds of simulation
/// time), and whether it is Dynamic or Static. A projection of
/// <see cref="NetSim.Core.Switching.MacAddressTableEntry"/> for the diagnostics view -
/// the switching engine stays out of the UI.
/// </summary>
public sealed class SwitchMacEntryRow
{
    public SwitchMacEntryRow(NetSim.Core.Switching.MacAddressTableEntry entry, System.TimeSpan now)
    {
        VlanText = entry.Vlan.Value.ToString();
        MacText = entry.MacAddress.ToString();
        PortText = entry.Port.Name;
        TypeText = entry.Type.ToString();
        AgeText = $"{(int)entry.Age(now).TotalSeconds}s";
    }

    /// <summary>The VLAN this entry was learned in (Phase 26).</summary>
    public string VlanText { get; }

    public string MacText { get; }

    public string PortText { get; }

    public string AgeText { get; }

    public string TypeText { get; }
}

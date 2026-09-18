using CommunityToolkit.Mvvm.ComponentModel;
using NetSim.UI.Common;

namespace NetSim.UI.Dialogs;

public sealed partial class DialogHost : ObservableObject, IDialogHost
{
    [ObservableProperty]
    private ViewModelBase? _activeDialog;
}

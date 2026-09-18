using System;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

/// <summary>Generic confirm/cancel dialog, e.g. "Delete 'My Network'? This action cannot be undone."</summary>
public partial class ConfirmationDialogViewModel : ViewModelBase
{
    private readonly Action _onConfirm;
    private readonly Action _onCancel;

    public ConfirmationDialogViewModel(
        string title,
        string message,
        string confirmText,
        string cancelText,
        bool isDestructive,
        Action onConfirm,
        Action onCancel)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = isDestructive;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool IsDestructive { get; }

    [RelayCommand]
    private void Confirm() => _onConfirm();

    [RelayCommand]
    private void Cancel() => _onCancel();
}

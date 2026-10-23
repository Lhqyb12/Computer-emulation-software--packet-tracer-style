using System.Threading.Tasks;
using NetSim.UI.ViewModels;

namespace NetSim.UI.Dialogs;

public sealed class DialogService : IDialogService
{
    private readonly IDialogHost _dialogHost;

    public DialogService(IDialogHost dialogHost)
    {
        _dialogHost = dialogHost;
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

    private void Complete<T>(TaskCompletionSource<T> tcs, T result)
    {
        _dialogHost.ActiveDialog = null;
        tcs.TrySetResult(result);
    }
}

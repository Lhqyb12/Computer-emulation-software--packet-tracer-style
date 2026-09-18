using System.Threading.Tasks;
using NetSim.Core.Networking;

namespace NetSim.UI.Dialogs;

/// <summary>
/// Shows modal dialogs and returns the user's input/decision asynchronously. Dialogs only
/// collect input and basic client-side validation (blank/too-long name) - the actual project
/// operation (and any server-side validation, e.g. duplicate names) is performed by the caller
/// through <see cref="Application.Services.IProjectService"/> after the dialog resolves, keeping
/// dialogs as simple input collectors rather than screens of their own.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows the "New Project" dialog. Returns null if the user cancelled.</summary>
    Task<NewProjectDialogResult?> ShowNewProjectAsync();

    /// <summary>Shows the "Rename Project" dialog, pre-filled with <paramref name="currentName"/>. Returns null if cancelled.</summary>
    Task<string?> ShowRenameProjectAsync(string currentName);

    /// <summary>Shows a generic confirm/cancel dialog. Returns true if the user confirmed.</summary>
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false);

    /// <summary>Shows the three-way "unsaved changes" dialog (Save / Don't Save / Cancel).</summary>
    Task<UnsavedChangesDecision> ShowUnsavedChangesAsync(string projectName);

    /// <summary>
    /// Shows the Ping diagnostic tool (Phase 21) for <paramref name="sourceInterface"/>. Unlike the
    /// other dialogs, this one is not a one-shot input collector - it stays open while the user runs
    /// pings, and the returned task resolves only once they close it.
    /// </summary>
    Task ShowPingAsync(NetworkInterface sourceInterface);

    /// <summary>
    /// Shows the DNS Lookup tool (Phase 23) for <paramref name="sourceInterface"/> - resolves a name
    /// the user types against the device's configured DNS server via <c>IDnsResolver</c>. Same
    /// stays-open shape as <see cref="ShowPingAsync"/>.
    /// </summary>
    Task ShowDnsLookupAsync(NetworkInterface sourceInterface);

    /// <summary>
    /// Shows the DHCP client tool (Phase 24) for <paramref name="sourceInterface"/> - requests /
    /// renews / releases a DHCP lease via <c>IDhcpClient</c> and shows the resulting configuration.
    /// Same stays-open shape as <see cref="ShowPingAsync"/>.
    /// </summary>
    Task ShowDhcpAsync(NetworkInterface sourceInterface);
}

public sealed record NewProjectDialogResult(string Name, string? Description);

public enum UnsavedChangesDecision
{
    Save,
    DontSave,
    Cancel,
}

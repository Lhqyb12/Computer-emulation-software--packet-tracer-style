using NetSim.UI.Common;

namespace NetSim.UI.Dialogs;

/// <summary>
/// Holds the ViewModel of the currently-open modal dialog, if any. <see cref="Views.MainWindow"/>
/// binds an overlay (using the "DialogScrim"/"DialogCard" styles from Theme/Styles/Dialogs.axaml,
/// prepared in Phase 5 for exactly this) to <see cref="ActiveDialog"/>, and the existing
/// <see cref="Views.ViewLocator"/> resolves its View the same way it resolves any screen. This
/// keeps dialogs as an in-window overlay rather than a separate OS window, and reuses the shell
/// this application already has instead of introducing a second UI mechanism.
/// </summary>
public interface IDialogHost
{
    ViewModelBase? ActiveDialog { get; set; }
}

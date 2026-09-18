namespace NetSim.UI.Theming;

/// <summary>
/// The set of appearance modes the application supports. <see cref="System"/> follows the OS
/// setting; persisting the user's choice across sessions is a future-phase concern (settings
/// storage does not exist yet) - for this phase the selection simply lives in memory for the
/// lifetime of the process, defaulting to <see cref="Dark"/>.
/// </summary>
public enum AppTheme
{
    Dark,
    Light,
    System
}

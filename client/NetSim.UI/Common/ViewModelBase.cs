using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetSim.UI.Common;

/// <summary>
/// Base class for every ViewModel in the application. Provides observable-property and
/// change-notification support via CommunityToolkit.Mvvm, plus the small set of UI-state
/// concerns (busy/error) that are common to almost any future screen.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Runs <paramref name="operation"/> with a consistent busy/error pattern: sets
    /// <see cref="IsBusy"/> while it runs, clears <see cref="ErrorMessage"/> beforehand, and
    /// captures any exception into <see cref="ErrorMessage"/> instead of letting it escape and
    /// crash the application. Re-entrant calls while already busy are ignored.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await operation().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected, user-initiated outcome, not an error to surface.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NetSim.UI.Common;

namespace NetSim.UI.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/> implementation. ViewModels are resolved through the
/// DI container so that each screen still receives its own dependencies via constructor
/// injection; the container is used purely as a typed factory here, and nowhere else in the
/// application does code reach into it directly.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<ViewModelBase> _backStack = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ViewModelBase? CurrentViewModel { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var next = _serviceProvider.GetRequiredService<TViewModel>();
        SetCurrent(next, pushCurrentToHistory: true);
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        var previous = _backStack.Pop();
        SetCurrent(previous, pushCurrentToHistory: false);
    }

    private void SetCurrent(ViewModelBase next, bool pushCurrentToHistory)
    {
        var previous = CurrentViewModel;

        if (ReferenceEquals(previous, next))
        {
            return;
        }

        if (previous is INavigationAware previousAware)
        {
            previousAware.OnNavigatedFrom();
        }

        if (pushCurrentToHistory && previous is not null)
        {
            _backStack.Push(previous);
        }

        CurrentViewModel = next;

        if (next is INavigationAware nextAware)
        {
            nextAware.OnNavigatedTo();
        }

        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}

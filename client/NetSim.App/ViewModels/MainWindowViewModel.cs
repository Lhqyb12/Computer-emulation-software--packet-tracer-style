namespace NetSim.App.ViewModels;

/// <summary>
/// The view model for the whole window. Its only job is <b>navigation</b>:
/// it holds whichever screen is currently visible (login or register) in
/// <see cref="CurrentPage"/>, and swaps it when the user asks to switch.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage = null!;

    public MainWindowViewModel()
    {
        ShowLogin();
    }

    /// <summary>The screen shown right now. The window binds its content to this.</summary>
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private void ShowLogin()
    {
        var login = new LoginViewModel();
        login.SwitchToRegisterRequested += ShowRegister;
        CurrentPage = login;
    }

    private void ShowRegister()
    {
        var register = new RegisterViewModel();
        register.SwitchToLoginRequested += ShowLogin;
        CurrentPage = register;
    }
}

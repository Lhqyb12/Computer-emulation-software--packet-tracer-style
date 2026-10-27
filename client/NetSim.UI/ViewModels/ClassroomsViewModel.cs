using System.Collections.ObjectModel;        // ObservableCollection - a list that tells the screen when it changes
using System.Threading.Tasks;                // Task
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.Application.Services;           // IClassroomService
using NetSim.Application.State;              // ICurrentUser
using NetSim.UI.Common;                      // ViewModelBase
using NetSim.UI.Dialogs;                     // IDialogService
using NetSim.UI.Navigation;                  // INavigationAware

namespace NetSim.UI.ViewModels;

// The "Classes" screen. Two halves:
//   - Classes I teach:  create a class, see its join code and students, remove a student, delete the class
//   - Classes I joined: join a class with a code, see who teaches it, leave it
// It reaches the classes only through IClassroomService - never through the repository directly
public partial class ClassroomsViewModel : ViewModelBase, INavigationAware
{
    private readonly IClassroomService _classroomService;   // the class rules and storage
    private readonly IDialogService _dialogService;         // the Create / Join / confirmation dialogs
    private readonly ICurrentUser _currentUser;             // who is signed in

    // The green line at the top after an action worked (null = hidden)
    [ObservableProperty]
    private string? _successMessage;

    // Purpose: Builds the screen and loads both lists.
    // Input:   the three services, passed in by dependency injection.
    // Output:  none (a constructor).
    public ClassroomsViewModel(IClassroomService classroomService, IDialogService dialogService, ICurrentUser currentUser)
    {
        _classroomService = classroomService;
        _dialogService = dialogService;
        _currentUser = currentUser;

        LoadClassrooms();
    }

    // The cards of the two halves of the screen
    public ObservableCollection<TeachingClassroomItem> TeachingClassrooms { get; } = new();
    public ObservableCollection<JoinedClassroomItem> JoinedClassrooms { get; } = new();

    // Used by the .axaml to show either the list or the "nothing here yet" box
    public bool HasTeachingClassrooms => TeachingClassrooms.Count > 0;
    public bool HasNoTeachingClassrooms => !HasTeachingClassrooms;
    public bool HasJoinedClassrooms => JoinedClassrooms.Count > 0;
    public bool HasNoJoinedClassrooms => !HasJoinedClassrooms;

    // Purpose: Opens the "Create a class" dialog and creates the class.
    // Input:   nothing.
    // Output:  a Task. Shows the new join code on success, or the error.
    // [RelayCommand] on "CreateClassroomAsync" generates "CreateClassroomCommand" (without "Async")
    [RelayCommand]
    private Task CreateClassroomAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;

        // Wait until the user closes the dialog. null = they pressed Cancel
        var name = await _dialogService.ShowCreateClassroomAsync();
        if (name is null)
            return;

        var result = _classroomService.CreateClassroom(name);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        // "!" = on success Value is never null
        SuccessMessage = $"'{result.Value!.Name}' was created. Give your students the code {result.Value.JoinCode}.";
        LoadClassrooms();
    });

    // Purpose: Opens the "Join a class" dialog and joins the class with that code.
    // Input:   nothing.
    // Output:  a Task. Shows which class was joined, or the error.
    [RelayCommand]
    private Task JoinClassroomAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;

        var code = await _dialogService.ShowJoinClassroomAsync();
        if (code is null)
            return;

        var result = _classroomService.JoinClassroom(code);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        SuccessMessage = $"You joined '{result.Value!.Name}'.";
        LoadClassrooms();
    });

    // Purpose: The teacher deletes one of their classes, after confirming.
    // Input:   item - the card whose delete button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task DeleteClassroomAsync(TeachingClassroomItem? item) => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        if (item is null)
            return;

        // Deleting can't be undone, so ask first. isDestructive = the confirm button is red
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Delete '{item.Name}'?",
            $"The class and its join code will be deleted. Its {item.StudentCountText} will no longer be in it.",
            confirmText: "Delete",
            isDestructive: true);
        if (!confirmed)
            return;

        var result = _classroomService.DeleteClassroom(item.Id);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        SuccessMessage = $"'{item.Name}' was deleted.";
        LoadClassrooms();
    });

    // Purpose: The teacher removes one student from a class, after confirming.
    // Input:   student - the student line whose "Remove" button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task RemoveStudentAsync(ClassroomStudentItem? student) => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        if (student is null)
            return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Remove {student.Email}?",
            "They will leave the class. They can join again only with the join code.",
            confirmText: "Remove",
            isDestructive: true);
        if (!confirmed)
            return;

        // The student line knows its class id - that's why ClassroomStudentItem carries it
        var result = _classroomService.RemoveStudent(student.ClassroomId, student.Email);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        SuccessMessage = $"{student.Email} was removed from the class.";
        LoadClassrooms();
    });

    // Purpose: The student leaves a class they joined, after confirming.
    // Input:   item - the card whose "Leave" button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task LeaveClassroomAsync(JoinedClassroomItem? item) => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        if (item is null)
            return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Leave '{item.Name}'?",
            "You can join again later with the join code.",
            confirmText: "Leave",
            isDestructive: true);
        if (!confirmed)
            return;

        var result = _classroomService.LeaveClassroom(item.Id);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        SuccessMessage = $"You left '{item.Name}'.";
        LoadClassrooms();
    });

    // Every time the screen is shown again (for example with "Back"), read the lists again
    void INavigationAware.OnNavigatedTo() => LoadClassrooms();

    void INavigationAware.OnNavigatedFrom()
    {
    }

    // Purpose: Reads both lists from the service and rebuilds the cards.
    // Input:   nothing.
    // Output:  nothing (fills TeachingClassrooms and JoinedClassrooms).
    private void LoadClassrooms()
    {
        TeachingClassrooms.Clear();
        foreach (var classroom in _classroomService.GetTeachingClassrooms())
            TeachingClassrooms.Add(new TeachingClassroomItem(classroom));

        JoinedClassrooms.Clear();
        foreach (var classroom in _classroomService.GetJoinedClassrooms())
            JoinedClassrooms.Add(new JoinedClassroomItem(classroom, _currentUser.Email));

        // The four Has... properties are computed from the lists, so the screen is told by hand
        OnPropertyChanged(nameof(HasTeachingClassrooms));
        OnPropertyChanged(nameof(HasNoTeachingClassrooms));
        OnPropertyChanged(nameof(HasJoinedClassrooms));
        OnPropertyChanged(nameof(HasNoJoinedClassrooms));
    }
}

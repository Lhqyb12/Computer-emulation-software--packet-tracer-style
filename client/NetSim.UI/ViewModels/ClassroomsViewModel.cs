using System.Collections.ObjectModel;        // ObservableCollection - a list that tells the screen when it changes
using System.Threading.Tasks;                // Task - "a job that will finish later"
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty] - writes the public property for a private field
using CommunityToolkit.Mvvm.Input;           // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Services;           // IClassroomService - the class rules
using NetSim.Application.State;              // ICurrentUser - who is signed in
using NetSim.UI.Common;                      // ViewModelBase - IsBusy, ErrorMessage, ExecuteAsync
using NetSim.UI.Dialogs;                     // IDialogService - the pop-up windows
using NetSim.UI.Navigation;                  // INavigationAware - "tell me when my screen is shown"

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Classes" screen. Two halves:
//   - Classes I teach:  create a class, see its join code and students, remove a student, delete the class
//   - Classes I joined: join a class with a code, see who teaches it, leave it
// It reaches the classes only through IClassroomService - never through the repository directly
public partial class ClassroomsViewModel : ViewModelBase, INavigationAware
{
    private readonly IClassroomService _classroomService;   // the class rules and storage
    private readonly IDialogService _dialogService;         // the Create / Join / confirmation dialogs
    private readonly ICurrentUser _currentUser;             // who is signed in

    // The green line at the top after an action worked (null = hidden).
    // [ObservableProperty] writes a public "SuccessMessage" property that tells the screen when it changes
    [ObservableProperty]
    private string? _successMessage;

    // Purpose: Builds the screen.
    // Input:   the three services, passed in by dependency injection.
    // Output:  none (a constructor).
    // The lists are NOT loaded here: a constructor can't wait for the server. The navigation service
    // calls OnNavigatedTo right after building this screen, and that loads them
    public ClassroomsViewModel(IClassroomService classroomService, IDialogService dialogService, ICurrentUser currentUser)
    {
        _classroomService = classroomService;   // keep the class service
        _dialogService = dialogService;         // keep the dialog service
        _currentUser = currentUser;             // keep the signed-in user
    }

    // The cards of the two halves of the screen. ObservableCollection tells the screen about every Add/Clear
    public ObservableCollection<TeachingClassroomItem> TeachingClassrooms { get; } = new();   // "Classes I teach"
    public ObservableCollection<JoinedClassroomItem> JoinedClassrooms { get; } = new();       // "Classes I joined"

    // Used by the .axaml to show either the list or the "nothing here yet" box
    public bool HasTeachingClassrooms => TeachingClassrooms.Count > 0;    // at least one class I teach
    public bool HasNoTeachingClassrooms => !HasTeachingClassrooms;        // none - show the empty box
    public bool HasJoinedClassrooms => JoinedClassrooms.Count > 0;        // at least one class I joined
    public bool HasNoJoinedClassrooms => !HasJoinedClassrooms;            // none - show the empty box

    // Purpose: Opens the "Create a class" dialog and creates the class.
    // Input:   nothing.
    // Output:  a Task. Shows the new join code on success, or the error.
    // [RelayCommand] on "CreateClassroomAsync" generates "CreateClassroomCommand" (without "Async").
    // ExecuteAsync (ViewModelBase) shows "busy", clears the old error, and turns exceptions into ErrorMessage
    [RelayCommand]
    private Task CreateClassroomAsync() => ExecuteAsync(async () =>
    {
        // Hide the green line of the previous action
        SuccessMessage = null;

        // Wait until the user closes the dialog. null = they pressed Cancel
        var name = await _dialogService.ShowCreateClassroomAsync();
        if (name is null)   // null = Cancel
            return;   // stop here - nothing more to do

        // Ask the service to create the class, and wait (it goes to the server)
        var result = await _classroomService.CreateClassroomAsync(name);

        // e.g. the name is empty - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // "!" = on success Value is never null. Tell the teacher the code to give the students
        SuccessMessage = $"'{result.Value!.Name}' was created. Give your students the code {result.Value.JoinCode}.";

        // Rebuild the cards so the new class appears
        await LoadClassroomsAsync();
    });

    // Purpose: Opens the "Join a class" dialog and joins the class with that code.
    // Input:   nothing.
    // Output:  a Task. Shows which class was joined, or the error.
    [RelayCommand]
    private Task JoinClassroomAsync() => ExecuteAsync(async () =>
    {
        // Hide the green line of the previous action
        SuccessMessage = null;

        // Wait for the code the student types. null = Cancel
        var code = await _dialogService.ShowJoinClassroomAsync();
        if (code is null)   // null = Cancel
            return;   // stop here - nothing more to do

        // Ask the service to join the class with this code, and wait
        var result = await _classroomService.JoinClassroomAsync(code);

        // e.g. no class has this code - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // Tell the student which class they joined
        SuccessMessage = $"You joined '{result.Value!.Name}'.";

        // Rebuild the cards so the class appears under "Classes I joined"
        await LoadClassroomsAsync();
    });

    // Purpose: The teacher deletes one of their classes, after confirming.
    // Input:   item - the card whose delete button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task DeleteClassroomAsync(TeachingClassroomItem? item) => ExecuteAsync(async () =>
    {
        // Hide the green line of the previous action
        SuccessMessage = null;

        // No card was passed - nothing to delete
        if (item is null)
            return;   // stop here - nothing more to do

        // Deleting can't be undone, so ask first. isDestructive = the confirm button is red
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Delete '{item.Name}'?",                                                                          // the title
            $"The class and its join code will be deleted. Its {item.StudentCountText} will no longer be in it.", // the message
            confirmText: "Delete",                                                                             // the confirm button's text
            isDestructive: true);                                                                              // paint it red

        // The user pressed Cancel
        if (!confirmed)
            return;   // stop here - nothing more to do

        // Ask the service to delete the class, and wait (the server checks that I am its teacher)
        var result = await _classroomService.DeleteClassroomAsync(item.Id);

        // e.g. I am not the teacher - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // Confirm it in green
        SuccessMessage = $"'{item.Name}' was deleted.";

        // Rebuild the cards so the class disappears
        await LoadClassroomsAsync();
    });

    // Purpose: The teacher removes one student from a class, after confirming.
    // Input:   student - the student line whose "Remove" button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task RemoveStudentAsync(ClassroomStudentItem? student) => ExecuteAsync(async () =>
    {
        // Hide the green line of the previous action
        SuccessMessage = null;

        // No student line was passed - nothing to remove
        if (student is null)
            return;   // stop here - nothing more to do

        // Ask first, with a red confirm button
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Remove {student.Email}?",                                                  // the title
            "They will leave the class. They can join again only with the join code.",  // the message
            confirmText: "Remove",                                                       // the confirm button's text
            isDestructive: true);                                                        // paint it red

        // The user pressed Cancel
        if (!confirmed)
            return;   // stop here - nothing more to do

        // The student line knows its class id - that's why ClassroomStudentItem carries it
        var result = await _classroomService.RemoveStudentAsync(student.ClassroomId, student.Email);

        // e.g. I am not the teacher of this class - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // Confirm it in green
        SuccessMessage = $"{student.Email} was removed from the class.";

        // Rebuild the cards so the student's line disappears
        await LoadClassroomsAsync();
    });

    // Purpose: The student leaves a class they joined, after confirming.
    // Input:   item - the card whose "Leave" button was clicked.
    // Output:  a Task.
    [RelayCommand]
    private Task LeaveClassroomAsync(JoinedClassroomItem? item) => ExecuteAsync(async () =>
    {
        // Hide the green line of the previous action
        SuccessMessage = null;

        // No card was passed - nothing to leave
        if (item is null)
            return;   // stop here - nothing more to do

        // Ask first, with a red confirm button
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Leave '{item.Name}'?",                         // the title
            "You can join again later with the join code.",  // the message
            confirmText: "Leave",                            // the confirm button's text
            isDestructive: true);                            // paint it red

        // The user pressed Cancel
        if (!confirmed)
            return;   // stop here - nothing more to do

        // Ask the service to take me out of the class, and wait
        var result = await _classroomService.LeaveClassroomAsync(item.Id);

        // e.g. I am not in this class - show why
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;   // stop here - nothing more to do
        }

        // Confirm it in green
        SuccessMessage = $"You left '{item.Name}'.";

        // Rebuild the cards so the class disappears
        await LoadClassroomsAsync();
    });

    // Purpose: Called by the navigation service every time this screen is shown - reads the lists again.
    // Input:   nothing.
    // Output:  nothing.
    // It can't wait (OnNavigatedTo is not async), so the load starts and finishes on its own.
    // "_ =" = start the Task without waiting for it. Not inside ExecuteAsync on purpose: the Home cards
    // open this screen and immediately "press" Create / Join - if the load marked the screen busy,
    // ExecuteAsync would ignore that press
    void INavigationAware.OnNavigatedTo() => _ = LoadClassroomsAsync();

    // Purpose: Called by the navigation service when the user leaves this screen.
    // Input:   nothing.
    // Output:  nothing. There is nothing to clean up, but the interface requires the method
    void INavigationAware.OnNavigatedFrom()
    {
    }

    // Purpose: Reads both lists from the service and rebuilds the cards.
    // Input:   nothing.
    // Output:  a Task that finishes when the cards are rebuilt (fills TeachingClassrooms and JoinedClassrooms).
    //          On a failure (e.g. the server is down) it shows the message and leaves the cards as they were.
    private async Task LoadClassroomsAsync()
    {
        // Ask the service for both halves at once, and wait
        var result = await _classroomService.GetMyClassroomsAsync();

        // Refused, or the server is down - show why, and stop
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        // Empty the "Classes I teach" cards
        TeachingClassrooms.Clear();

        // One card for every class I teach. "!" = on success Value is never null
        foreach (var classroom in result.Value!.Teaching)
            TeachingClassrooms.Add(new TeachingClassroomItem(classroom));

        // Empty the "Classes I joined" cards
        JoinedClassrooms.Clear();

        // One card for every class I joined (my email finds my own join date inside it)
        foreach (var classroom in result.Value.Joined)
            JoinedClassrooms.Add(new JoinedClassroomItem(classroom, _currentUser.Email));

        // The four Has... properties are computed from the lists, so the screen is told by hand
        OnPropertyChanged(nameof(HasTeachingClassrooms));   // tell the screen that HasTeachingClassrooms may have changed
        OnPropertyChanged(nameof(HasNoTeachingClassrooms));   // tell the screen that HasNoTeachingClassrooms may have changed
        OnPropertyChanged(nameof(HasJoinedClassrooms));   // tell the screen that HasJoinedClassrooms may have changed
        OnPropertyChanged(nameof(HasNoJoinedClassrooms));   // tell the screen that HasNoJoinedClassrooms may have changed
    }
}

using NetSim.Application.Common;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Application.Tests.TestSupport;
using NetSim.Core.Common;

namespace NetSim.Application.Tests.Services;

public class ProjectServiceTests
{
    private static ProjectService CreateService()
    {
        var applicationState = new ApplicationState();
        return new ProjectService(applicationState, new InMemoryProjectRepository(), new NetworkService(applicationState));
    }

    private static (ProjectService Service, IApplicationState ApplicationState) CreateServiceWithState()
    {
        var applicationState = new ApplicationState();
        var service = new ProjectService(applicationState, new InMemoryProjectRepository(), new NetworkService(applicationState));
        return (service, applicationState);
    }

    [Fact]
    public void CreateProject_ValidName_Succeeds_AndBecomesCurrentProject()
    {
        var service = CreateService();

        var result = service.CreateProject("Campus Network", "A demo project");

        Assert.True(result.IsSuccess);
        Assert.Equal("Campus Network", result.Value!.Name);
        Assert.Equal("A demo project", result.Value.Description);
        Assert.Same(result.Value, service.CurrentProject);
        Assert.False(service.IsCurrentProjectDirty);
    }

    [Fact]
    public void CreateProject_BlankName_ReturnsValidationError()
    {
        var service = CreateService();

        var result = service.CreateProject("   ", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
    }

    [Fact]
    public void CreateProject_NameTooLong_ReturnsValidationError()
    {
        var service = CreateService();

        var result = service.CreateProject(new string('a', 101), null);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
    }

    [Fact]
    public void CreateProject_DuplicateName_ReturnsConflict()
    {
        var service = CreateService();
        service.CreateProject("Campus Network", null);

        var result = service.CreateProject("campus network", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public void GetAllProjects_ReturnsEveryCreatedProject()
    {
        var service = CreateService();
        service.CreateProject("Net A", null);
        service.CreateProject("Net B", null);

        var projects = service.GetAllProjects();

        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, p => p.Name == "Net A");
        Assert.Contains(projects, p => p.Name == "Net B");
    }

    [Fact]
    public void OpenProject_ThatExists_BecomesCurrentProject_AndRecordsLastOpened()
    {
        var service = CreateService();
        var created = service.CreateProject("Net A", null).Value!;
        service.CloseCurrentProject();

        var result = service.OpenProject(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(result.Value, service.CurrentProject);
        Assert.NotNull(result.Value!.LastOpenedAtUtc);
        Assert.False(service.IsCurrentProjectDirty);
    }

    [Fact]
    public void OpenProject_UnknownId_ReturnsNotFound()
    {
        var service = CreateService();

        var result = service.OpenProject(EntityId.New());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void SaveCurrentProject_NoProjectOpen_ReturnsInvalidState()
    {
        var service = CreateService();

        var result = service.SaveCurrentProject();

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void SaveCurrentProject_UpdatesModifiedTimestamp_AndClearsDirtyFlag()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;
        var originalModified = project.LastModifiedAtUtc;
        service.UpdateCurrentProjectDescription("changed");
        Assert.True(service.IsCurrentProjectDirty);

        var result = service.SaveCurrentProject();

        Assert.True(result.IsSuccess);
        Assert.False(service.IsCurrentProjectDirty);
        Assert.True(project.LastModifiedAtUtc >= originalModified);
    }

    [Fact]
    public void SaveCurrentProject_PersistsChanges_VisibleAfterReopening()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;
        service.UpdateCurrentProjectDescription("changed");
        service.SaveCurrentProject();
        service.CloseCurrentProject();

        var reopened = service.OpenProject(project.Id).Value!;

        Assert.Equal("changed", reopened.Description);
    }

    [Fact]
    public void SaveCurrentProjectAs_NoProjectOpen_ReturnsInvalidState()
    {
        var service = CreateService();

        var result = service.SaveCurrentProjectAs("Copy");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void SaveCurrentProjectAs_CreatesNewProject_WithNewIdAndTimestamps_LeavingOriginalIntact()
    {
        var service = CreateService();
        var original = service.CreateProject("Net A", "desc").Value!;

        var result = service.SaveCurrentProjectAs("Net A Copy");

        Assert.True(result.IsSuccess);
        var copy = result.Value!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Net A Copy", copy.Name);
        Assert.Equal(original.Description, copy.Description);
        Assert.Same(copy, service.CurrentProject);

        var allProjects = service.GetAllProjects();
        Assert.Equal(2, allProjects.Count);
        var reloadedOriginal = allProjects.Single(p => p.Id.Equals(original.Id));
        Assert.Equal("Net A", reloadedOriginal.Name);
    }

    [Fact]
    public void SaveCurrentProjectAs_DuplicateName_ReturnsConflict_AndDoesNotChangeCurrentProject()
    {
        var service = CreateService();
        service.CreateProject("Net A", null);
        var second = service.CreateProject("Net B", null).Value!;

        var result = service.SaveCurrentProjectAs("Net A");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.Conflict, result.ErrorType);
        Assert.Same(second, service.CurrentProject);
    }

    [Fact]
    public void RenameProject_ThatExists_UpdatesNameAndPersists_KeepsSameId()
    {
        var service = CreateService();
        var project = service.CreateProject("Old Name", null).Value!;

        var result = service.RenameProject(project.Id, "New Name");

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", project.Name);
        var reloaded = service.GetAllProjects().Single();
        Assert.Equal(project.Id, reloaded.Id);
        Assert.Equal("New Name", reloaded.Name);
    }

    [Fact]
    public void RenameProject_CurrentProject_UpdatesTheSameInstance_AndStatusReflectsIt()
    {
        var service = CreateService();
        var project = service.CreateProject("Old Name", null).Value!;

        service.RenameProject(project.Id, "New Name");

        Assert.Same(project, service.CurrentProject);
        Assert.Equal("New Name", service.CurrentProject!.Name);
    }

    [Fact]
    public void RenameProject_UnknownId_ReturnsNotFound()
    {
        var service = CreateService();

        var result = service.RenameProject(EntityId.New(), "New Name");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void RenameProject_BlankName_ReturnsValidationError()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;

        var result = service.RenameProject(project.Id, "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
        Assert.Equal("Net A", project.Name);
    }

    [Fact]
    public void RenameProject_ToAnotherProjectsName_ReturnsConflict()
    {
        var service = CreateService();
        service.CreateProject("Net A", null);
        var second = service.CreateProject("Net B", null).Value!;

        var result = service.RenameProject(second.Id, "Net A");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public void RenameProject_ToItsOwnCurrentName_Succeeds()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;

        var result = service.RenameProject(project.Id, "Net A");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DeleteProject_ThatExists_RemovesItFromTheList()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;
        service.CloseCurrentProject();

        var result = service.DeleteProject(project.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(service.GetAllProjects());
    }

    [Fact]
    public void DeleteProject_UnknownId_ReturnsNotFound()
    {
        var service = CreateService();

        var result = service.DeleteProject(EntityId.New());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void DeleteProject_ThatIsCurrentlyOpen_ClearsCurrentProjectState()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;

        var result = service.DeleteProject(project.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(service.CurrentProject);
    }

    [Fact]
    public void DeleteProject_NotCurrentlyOpen_DoesNotAffectCurrentProject()
    {
        var service = CreateService();
        var current = service.CreateProject("Net A", null).Value!;
        var other = service.CreateProject("Net B", null).Value!;
        // Creating "Net B" made it current; reopen "Net A" so "Net B" is the one being deleted below.
        service.OpenProject(current.Id);

        var result = service.DeleteProject(other.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(current, service.CurrentProject);
    }

    [Fact]
    public void CloseCurrentProject_ClearsCurrentProject_AndDirtyFlag()
    {
        var service = CreateService();
        service.CreateProject("Net A", null);
        service.UpdateCurrentProjectDescription("changed");

        service.CloseCurrentProject();

        Assert.Null(service.CurrentProject);
        Assert.False(service.IsCurrentProjectDirty);
    }

    [Fact]
    public void CloseCurrentProject_NoProjectOpen_IsANoOp()
    {
        var service = CreateService();

        service.CloseCurrentProject();

        Assert.Null(service.CurrentProject);
    }

    [Fact]
    public void UpdateCurrentProjectDescription_NoProjectOpen_ReturnsInvalidState()
    {
        var service = CreateService();

        var result = service.UpdateCurrentProjectDescription("desc");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void DirtyState_CreateProject_StartsClean_UpdateMarksDirty_SaveClearsIt()
    {
        var service = CreateService();
        var project = service.CreateProject("Net A", null).Value!;
        Assert.False(service.IsCurrentProjectDirty);

        service.UpdateCurrentProjectDescription("changed");
        Assert.True(service.IsCurrentProjectDirty);
        Assert.Equal("changed", project.Description);

        service.SaveCurrentProject();
        Assert.False(service.IsCurrentProjectDirty);
    }

    [Fact]
    public void CurrentProjectChanged_And_DirtyStateChanged_AreRaised()
    {
        var service = CreateService();
        var currentProjectChangedCount = 0;
        var dirtyStateChangedCount = 0;
        service.CurrentProjectChanged += (_, _) => currentProjectChangedCount++;
        service.DirtyStateChanged += (_, _) => dirtyStateChangedCount++;

        service.CreateProject("Net A", null);
        service.UpdateCurrentProjectDescription("changed");
        service.SaveCurrentProject();

        Assert.Equal(1, currentProjectChangedCount);
        Assert.Equal(2, dirtyStateChangedCount); // dirty -> true, then -> false
    }

    // Project metadata does not persist a topology yet (see docs/architecture/device-model.md),
    // so ProjectService is responsible for making sure a network always exists once a project is
    // open - otherwise the Device Library (Phase 11) would have nowhere to add devices to.

    [Fact]
    public void CreateProject_AlsoCreatesAnEmptyCurrentNetwork()
    {
        var (service, applicationState) = CreateServiceWithState();

        service.CreateProject("Campus Network", null);

        Assert.NotNull(applicationState.CurrentNetwork);
        Assert.Empty(applicationState.CurrentNetwork.Devices);
    }

    [Fact]
    public void OpenProject_ReplacesAnyExistingNetworkWithAFreshEmptyOne()
    {
        var (service, applicationState) = CreateServiceWithState();
        var a = service.CreateProject("Net A", null).Value!;
        var b = service.CreateProject("Net B", null).Value!;
        applicationState.CurrentNetwork!.AddDevice(new Core.Devices.Router("R1"));

        service.OpenProject(a.Id);

        Assert.NotNull(applicationState.CurrentNetwork);
        Assert.Empty(applicationState.CurrentNetwork.Devices);
    }

    [Fact]
    public void CloseCurrentProject_ClearsCurrentNetwork()
    {
        var (service, applicationState) = CreateServiceWithState();
        service.CreateProject("Net A", null);

        service.CloseCurrentProject();

        Assert.Null(applicationState.CurrentNetwork);
    }

    [Fact]
    public void DeleteProject_WhenItIsTheCurrentProject_ClearsCurrentNetwork()
    {
        var (service, applicationState) = CreateServiceWithState();
        var project = service.CreateProject("Net A", null).Value!;

        service.DeleteProject(project.Id);

        Assert.Null(applicationState.CurrentNetwork);
    }

    [Fact]
    public void SaveCurrentProjectAs_DoesNotResetTheCurrentNetwork()
    {
        var (service, applicationState) = CreateServiceWithState();
        service.CreateProject("Net A", null);
        var device = new Core.Devices.Router("R1");
        applicationState.CurrentNetwork!.AddDevice(device);

        service.SaveCurrentProjectAs("Net A Copy");

        Assert.Same(device, Assert.Single(applicationState.CurrentNetwork!.Devices));
    }
}

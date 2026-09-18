using NetSim.Application.Projects;
using NetSim.Application.State;
using NetSim.Core.Common;
using NetSim.Core.Topology;

namespace NetSim.Application.Tests.State;

public class ApplicationStateTests
{
    [Fact]
    public void CurrentNetwork_IsNull_Initially()
    {
        var state = new ApplicationState();

        Assert.Null(state.CurrentNetwork);
    }

    [Fact]
    public void SetCurrentNetwork_UpdatesCurrentNetwork_AndRaisesEvent()
    {
        var state = new ApplicationState();
        var network = new Network("Test Network");
        var raised = false;
        state.CurrentNetworkChanged += (_, _) => raised = true;

        state.SetCurrentNetwork(network);

        Assert.Same(network, state.CurrentNetwork);
        Assert.True(raised);
    }

    [Fact]
    public void SetCurrentNetwork_SameReference_DoesNotRaiseEvent()
    {
        var state = new ApplicationState();
        var network = new Network("Test Network");
        state.SetCurrentNetwork(network);
        var raised = false;
        state.CurrentNetworkChanged += (_, _) => raised = true;

        state.SetCurrentNetwork(network);

        Assert.False(raised);
    }

    [Fact]
    public void SetCurrentNetwork_Null_ClearsCurrentNetwork()
    {
        var state = new ApplicationState();
        state.SetCurrentNetwork(new Network("Test Network"));

        state.SetCurrentNetwork(null);

        Assert.Null(state.CurrentNetwork);
    }

    private static Project CreateProject(string name = "Net A") =>
        new(EntityId.New(), name, null, DateTime.UtcNow, DateTime.UtcNow, null, Project.CurrentSchemaVersion);

    [Fact]
    public void CurrentProject_IsNull_Initially()
    {
        var state = new ApplicationState();

        Assert.Null(state.CurrentProject);
        Assert.False(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void SetCurrentProject_UpdatesCurrentProject_AndRaisesEvent()
    {
        var state = new ApplicationState();
        var project = CreateProject();
        var raised = false;
        state.CurrentProjectChanged += (_, _) => raised = true;

        state.SetCurrentProject(project);

        Assert.Same(project, state.CurrentProject);
        Assert.True(raised);
    }

    [Fact]
    public void SetCurrentProject_SameReference_DoesNotRaiseCurrentProjectChanged()
    {
        var state = new ApplicationState();
        var project = CreateProject();
        state.SetCurrentProject(project);
        var raised = false;
        state.CurrentProjectChanged += (_, _) => raised = true;

        state.SetCurrentProject(project);

        Assert.False(raised);
    }

    [Fact]
    public void SetCurrentProject_AlwaysResetsDirtyFlag_EvenForTheSameProject()
    {
        var state = new ApplicationState();
        var project = CreateProject();
        state.SetCurrentProject(project);
        state.MarkCurrentProjectDirty();

        state.SetCurrentProject(project);

        Assert.False(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void MarkCurrentProjectDirty_WithoutACurrentProject_IsANoOp()
    {
        var state = new ApplicationState();

        state.MarkCurrentProjectDirty();

        Assert.False(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void MarkCurrentProjectDirty_ThenClean_RaisesDirtyStateChanged_ForEachTransition()
    {
        var state = new ApplicationState();
        state.SetCurrentProject(CreateProject());
        var raisedCount = 0;
        state.IsCurrentProjectDirtyChanged += (_, _) => raisedCount++;

        state.MarkCurrentProjectDirty();
        state.MarkCurrentProjectDirty(); // no-op: already dirty
        state.MarkCurrentProjectClean();

        Assert.Equal(2, raisedCount);
        Assert.False(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void SetCurrentProject_Null_ClearsCurrentProject()
    {
        var state = new ApplicationState();
        state.SetCurrentProject(CreateProject());

        state.SetCurrentProject(null);

        Assert.Null(state.CurrentProject);
    }
}

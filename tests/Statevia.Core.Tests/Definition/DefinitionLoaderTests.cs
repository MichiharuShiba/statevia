using Statevia.Core.Definition;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class DefinitionLoaderTests
{
    /// <summary>DefinitionLoader が YAML をパースし、workflow/states/fork/join を正しく読み込むことを検証する。</summary>
    [Fact]
    public void Load_ParsesHelloWorkflow()
    {
        // Arrange: Hello ワークフロー相当の YAML
        var yaml = """
            workflow:
              name: HelloWorkflow

            states:
              Start:
                on:
                  Completed:
                    fork: [Prepare, AskUser]
              Prepare:
                on:
                  Completed:
                    next: Join1
              Join1:
                join:
                  allOf: [Prepare, AskUser]
                on:
                  Joined:
                    next: Work
            """;

        // Act
        var loader = new DefinitionLoader();
        var def = loader.Load(yaml);

        // Assert
        Assert.Equal("HelloWorkflow", def.Workflow.Name);
        Assert.Equal(3, def.States.Count);
        Assert.True(def.States.ContainsKey("Start"));
        Assert.True(def.States.ContainsKey("Prepare"));
        Assert.True(def.States.ContainsKey("Join1"));

        var start = def.States["Start"];
        Assert.NotNull(start.On);
        var fork = start.On!["Completed"].Fork;
        Assert.NotNull(fork);
        Assert.Contains("Prepare", fork!);
        Assert.Contains("AskUser", fork!);

        var join1 = def.States["Join1"];
        Assert.NotNull(join1.Join);
        Assert.Equal(2, join1.Join!.AllOf.Count);
    }

    /// <summary>wait のみの状態をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateWithWaitOnly()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              WaitState:
                wait:
                  event: resume
            """;
        var loader = new DefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["WaitState"];

        // Assert
        Assert.NotNull(state.Wait);
        Assert.Equal("resume", state.Wait!.Event);
    }

    /// <summary>join のみの状態をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateWithJoinOnly()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              JoinOnly:
                join:
                  allOf: [A, B]
            """;
        var loader = new DefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["JoinOnly"];

        // Assert
        Assert.NotNull(state.Join);
        Assert.Equal(2, state.Join!.AllOf.Count);
        Assert.Contains("A", state.Join.AllOf);
        Assert.Contains("B", state.Join.AllOf);
    }

    /// <summary>end: true の遷移をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesTransitionWithEndTrue()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              EndState:
                on:
                  Completed:
                    end: true
            """;
        var loader = new DefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["EndState"];

        // Assert
        Assert.NotNull(state.On);
        Assert.True(state.On!["Completed"].End);
    }

    /// <summary>states が空の YAML をパースすることを検証する。</summary>
    [Fact]
    public void Load_WithEmptyStates_ReturnsEmptyStates()
    {
        // Arrange
        var yaml = """
            workflow:
              name: Empty
            states: {}
            """;
        var loader = new DefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.NotNull(def.States);
        Assert.Empty(def.States);
    }
}

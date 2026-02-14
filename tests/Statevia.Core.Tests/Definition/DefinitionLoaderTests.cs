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
}

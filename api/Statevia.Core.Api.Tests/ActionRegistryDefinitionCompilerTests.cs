using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Api.Tests;

public sealed class ActionRegistryDefinitionCompilerTests
{
    private static DefinitionCompilerService CreateSut(IActionRegistry? registry = null)
    {
        registry ??= new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        return new DefinitionCompilerService(registry);
    }

    [Fact]
    public void ValidateAndCompile_UnknownAction_ThrowsWithMessage()
    {
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: missing.action
                on:
                  Completed:
                    end: true
            """;

        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("Unknown action 'missing.action'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("state 'A'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndCompile_WaitWithoutAction_Succeeds()
    {
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                wait:
                  event: E
                on:
                  Completed:
                    end: true
            """;

        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        Assert.NotNull(compiled);
        var exec = compiled.StateExecutorFactory.GetExecutor("A");
        Assert.NotNull(exec);
    }

    [Fact]
    public void ValidateAndCompile_RegisteredCustomAction_Succeeds()
    {
        var registry = new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        registry.Register(
            "custom.echo",
            new DefaultStateExecutor((_, input, _) => Task.FromResult<object?>(input)));
        var svc = new DefinitionCompilerService(registry);
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: custom.echo
                on:
                  Completed:
                    end: true
            """;

        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        var exec = compiled.StateExecutorFactory.GetExecutor("A");
        Assert.NotNull(exec);
    }

    [Fact]
    public async Task Start_CustomEchoAction_OutputReflectsWorkflowInput()
    {
        var registry = new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        registry.Register(
            "custom.echo",
            new DefaultStateExecutor((_, input, _) => Task.FromResult<object?>(input)));
        var compiler = new DefinitionCompilerService(registry);
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: custom.echo
                on:
                  Completed:
                    end: true
            """;
        var (def, _) = compiler.ValidateAndCompile("W", yaml);

        var engine = new WorkflowEngine();
        var wfId = engine.Start(def, workflowInput: new Dictionary<string, int> { ["x"] = 42 });

        await Task.Delay(200);

        var json = engine.ExportExecutionGraph(wfId);
        Assert.Contains("42", json, StringComparison.Ordinal);
    }
}

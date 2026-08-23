using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary><see cref="WorkflowActionState"/> の入力検証と runner 委譲。</summary>
public sealed class WorkflowActionStateTests
{
    /// <summary>runner 結果を辞書として返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDefinitionIdPresent_ReturnsRunnerResult()
    {
        // Arrange
        var runner = new FakeChildWorkflowRunner
        {
            Result = new ChildWorkflowResult("exec-1", "wf-1", "Running")
        };
        var sut = new WorkflowActionState(BuildScopeFactory(runner));
        var input = JsonSerializer.SerializeToElement(new
        {
            definitionId = "def-1",
            input = new { foo = "bar" }
        });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("exec-1", map["workflowId"]);
        Assert.Equal("wf-1", map["displayId"]);
        Assert.Equal("Running", map["status"]);
        Assert.Equal("def-1", runner.LastRequest!.DefinitionId);
        Assert.NotEqual(Guid.Empty, runner.LastRequest.ParentExecutionId);
    }

    /// <summary>入力がオブジェクトでないとき ArgumentException。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenInputNotObject_Throws()
    {
        // Arrange
        var sut = new WorkflowActionState(BuildScopeFactory(new FakeChildWorkflowRunner()));
        var input = JsonSerializer.SerializeToElement("plain");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(MakeContext(), input, CancellationToken.None));
        Assert.Contains("definitionId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>親実行 ID が GUID でないときは開始しない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenExecutionIdInvalid_Throws()
    {
        // Arrange
        var sut = new WorkflowActionState(BuildScopeFactory(new FakeChildWorkflowRunner()));
        var input = JsonSerializer.SerializeToElement(new { definitionId = "d1" });
        var ctx = MakeContext("not-a-guid");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(ctx, input, CancellationToken.None));
        Assert.Contains("parent execution id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IServiceScopeFactory BuildScopeFactory(IChildWorkflowRunner runner)
    {
        var services = new ServiceCollection();
        services.AddSingleton(runner);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static StateContext MakeContext(string? executionId = null) =>
        new()
        {
            Events = new NoopEvents(),
            Store = new NoopStore(),
            ExecutionId = executionId ?? Guid.NewGuid().ToString("D"),
            StateName = "Workflow"
        };

    private sealed class FakeChildWorkflowRunner : IChildWorkflowRunner
    {
        public ChildWorkflowResult Result { get; init; } = new("id", "disp", "Running");
        public ChildWorkflowRequest? LastRequest { get; private set; }

        public Task<ChildWorkflowResult> RunAsync(ChildWorkflowRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class NoopEvents : IEventProvider
    {
        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;
        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);
        public void Signal(string signalName) { }
        public void Resume(string nodeId, string eventName) { }
        public void PublishTopic(string topic, object? payloadSummary) { }
    }

    private sealed class NoopStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }
}

using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="OutOfProcessBackend"/> の単体テスト。</summary>
public sealed class OutOfProcessBackendTests
{
    /// <summary>Action Host クライアントへ委譲する。</summary>
    [Fact]
    public async Task ExecuteAsync_DelegatesToClient()
    {
        // Arrange
        var client = new RecordingActionHostExecutionClient();
        var sut = new OutOfProcessBackend(client);
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-oop-1",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act
        var result = await sut.ExecuteAsync(request, runtimeInput: new { value = 1 }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(client.LastRequest);
        Assert.NotNull(client.LastRequest!.Input);
    }

    private sealed class RecordingActionHostExecutionClient : IActionHostExecutionClient
    {
        public ActionExecutionRequest? LastRequest { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ActionExecutionResult { Success = true });
        }
    }
}

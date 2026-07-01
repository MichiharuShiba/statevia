using Statevia.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="OutOfProcessBackend"/> の単体テスト。</summary>
public sealed class OutOfProcessBackendTests
{
    /// <summary>Mode と ProviderKey は OutOfProcess 契約を公開する。</summary>
    [Fact]
    public void Metadata_ExposesOutOfProcessContract()
    {
        // Arrange
        var sut = new OutOfProcessBackend(new RecordingActionHostExecutionClient());

        // Act / Assert
        Assert.Equal(ActionExecutionMode.OutOfProcess, sut.Mode);
        Assert.Equal("action-host", sut.ProviderKey);
    }

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
        var invocation = new ActionBackendInvocation(request, RuntimeInput: new { value = 1 });

        // Act
        var result = await sut.ExecuteAsync(invocation, CancellationToken.None);

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

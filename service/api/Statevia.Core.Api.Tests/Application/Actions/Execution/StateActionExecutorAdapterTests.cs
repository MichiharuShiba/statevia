using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="StateActionExecutorAdapter"/> の単体テスト。</summary>
public sealed class StateActionExecutorAdapterTests
{
    private sealed class StubActionExecutor : IActionExecutor
    {
        private readonly ActionExecutionResult _result;

        public StubActionExecutor(ActionExecutionResult result) => _result = result;

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            StateContext stateContext,
            object? runtimeInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private static StateContext CreateContext() =>
        new()
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

    /// <summary>成功結果の RuntimeOutput を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSuccess_ReturnsRuntimeOutput()
    {
        // Arrange
        var expected = new { value = 42 };
        var sut = new StateActionExecutorAdapter(
            "test.action",
            ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            new StubActionExecutor(new ActionExecutionResult
            {
                Success = true,
                RuntimeOutput = expected,
            }));
        var ctx = CreateContext();

        // Act
        var output = await sut.ExecuteAsync(ctx, null, CancellationToken.None);

        // Assert
        Assert.Same(expected, output);
    }

    /// <summary>失敗結果は InvalidOperationException に変換する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = new StateActionExecutorAdapter(
            "test.action",
            ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            new StubActionExecutor(new ActionExecutionResult
            {
                Success = false,
                ErrorCode = "TestError",
                ErrorMessage = "execution failed",
            }));
        var ctx = CreateContext();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(ctx, null, CancellationToken.None));
        Assert.Equal("execution failed", ex.Message);
    }
}

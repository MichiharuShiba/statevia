using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Actions.Abstractions.Execution;
using Statevia.ActionHost.Execution;
using Statevia.ActionHost.Modules;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.ActionHost.Tests;

/// <summary><see cref="ActionHostExecutor"/> の単体テスト。</summary>
public sealed class ActionHostExecutorTests
{
    /// <summary>期限切れ deadline は DeadlineExceeded を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlinePassed_ReturnsDeadlineExceeded()
    {
        // Arrange
        var executor = CreateExecutor(CreateEchoRegistration("test.module.echo"));

        // Act
        var result = await executor.ExecuteAsync(
            new ActionExecutionRequest
            {
                ExecutionId = "exec-deadline",
                StateName = "Echo",
                ActionId = "test.module.echo",
                TenantId = "00000000-0000-4000-8000-000000000001",
                Deadline = DateTimeOffset.UtcNow.AddSeconds(-5),
            },
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("DeadlineExceeded", result.ErrorCode);
    }

    /// <summary>キャンセル済みトークンは Cancelled を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var executor = CreateExecutor(CreateSlowRegistration("test.module.slow"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await executor.ExecuteAsync(
            new ActionExecutionRequest
            {
                ExecutionId = "exec-cancel",
                StateName = "Slow",
                ActionId = "test.module.slow",
                TenantId = "00000000-0000-4000-8000-000000000001",
            },
            cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.ErrorCode);
    }

    /// <summary>Action 実行失敗は ActionExecutionFailed を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionThrows_ReturnsActionExecutionFailed()
    {
        // Arrange
        var executor = CreateExecutor(CreateFailingRegistration("test.module.fail"));

        // Act
        var result = await executor.ExecuteAsync(
            new ActionExecutionRequest
            {
                ExecutionId = "exec-fail",
                StateName = "Fail",
                ActionId = "test.module.fail",
                TenantId = "00000000-0000-4000-8000-000000000001",
            },
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ActionExecutionFailed", result.ErrorCode);
        Assert.Equal("boom", result.ErrorMessage);
    }

    /// <summary>非 JsonElement 出力は JSON にシリアライズされる。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOutputIsPlainObject_SerializesToJson()
    {
        // Arrange
        var executor = CreateExecutor(CreateObjectOutputRegistration("test.module.object"));

        // Act
        var result = await executor.ExecuteAsync(
            new ActionExecutionRequest
            {
                ExecutionId = "exec-object",
                StateName = "Object",
                ActionId = "test.module.object",
                TenantId = "00000000-0000-4000-8000-000000000001",
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal("ok", result.Output.Value.GetProperty("status").GetString());
    }

    /// <summary>null 出力は Output なしで成功する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOutputIsNull_ReturnsSuccessWithoutOutput()
    {
        // Arrange
        var executor = CreateExecutor(CreateNullOutputRegistration("test.module.null"));

        // Act
        var result = await executor.ExecuteAsync(
            new ActionExecutionRequest
            {
                ExecutionId = "exec-null",
                StateName = "Null",
                ActionId = "test.module.null",
                TenantId = "00000000-0000-4000-8000-000000000001",
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Output);
    }

    private static ActionHostExecutor CreateExecutor(LoadedActionRegistration registration)
    {
        var registry = new ActionHostActionRegistry();
        Assert.True(registry.TryRegister(registration));
        return new ActionHostExecutor(registry, NullLogger<ActionHostExecutor>.Instance);
    }

    private static LoadedActionRegistration CreateEchoRegistration(string actionId) =>
        new(actionId, DefaultStateExecutor.Create(new EchoState()), "test.module");

    private static LoadedActionRegistration CreateSlowRegistration(string actionId) =>
        new(actionId, DefaultStateExecutor.Create(new SlowState()), "test.module");

    private static LoadedActionRegistration CreateFailingRegistration(string actionId) =>
        new(actionId, DefaultStateExecutor.Create(new FailingState()), "test.module");

    private static LoadedActionRegistration CreateObjectOutputRegistration(string actionId) =>
        new(actionId, DefaultStateExecutor.Create(new ObjectOutputState()), "test.module");

    private static LoadedActionRegistration CreateNullOutputRegistration(string actionId) =>
        new(actionId, DefaultStateExecutor.Create(new NullOutputState()), "test.module");

    private sealed class EchoState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    private sealed class SlowState : IState<object?, object?>
    {
        public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return input;
        }
    }

    private sealed class FailingState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class ObjectOutputState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult<object?>(new { status = "ok" });
    }

    private sealed class NullOutputState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult<object?>(null);
    }
}

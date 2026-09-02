using Grpc.Core;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc.Contracts;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="ActionHostGrpcInvoker"/> の gRPC deadline と例外写像。</summary>
public sealed class ActionHostGrpcInvokerTests
{
    /// <summary>Deadline があるとき RPC に UTC deadline を渡す。</summary>
    [Fact]
    public async Task InvokeAsync_WithDeadline_PassesUtcDeadline()
    {
        // Arrange
        var deadline = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        DateTime? capturedDeadline = null;
        var request = CreateRequest(deadline);

        // Act
        var result = await ActionHostGrpcInvoker.InvokeAsync(
            (_, grpcDeadline, _) =>
            {
                capturedDeadline = grpcDeadline;
                return Task.FromResult(new ActionExecutionRpcResponse { Success = true });
            },
            request,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(deadline.UtcDateTime, capturedDeadline);
    }

    /// <summary>Deadline が無いとき gRPC deadline を付けない。</summary>
    [Fact]
    public async Task InvokeAsync_WithoutDeadline_DoesNotSetGrpcDeadline()
    {
        // Arrange
        DateTime? capturedDeadline = DateTime.MaxValue;
        var request = CreateRequest(deadline: null);

        // Act
        var result = await ActionHostGrpcInvoker.InvokeAsync(
            (_, grpcDeadline, _) =>
            {
                capturedDeadline = grpcDeadline;
                return Task.FromResult(new ActionExecutionRpcResponse { Success = true });
            },
            request,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(capturedDeadline);
    }

    /// <summary>Host が deadline を超えると DeadlineExceeded になる。</summary>
    [Fact]
    public async Task InvokeAsync_DeadlineExceeded_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest(DateTimeOffset.UtcNow);

        // Act
        var result = await ActionHostGrpcInvoker.InvokeAsync(
            (_, _, _) => throw new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout")),
            request,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("DeadlineExceeded", result.ErrorCode);
    }

    private static ActionExecutionRequest CreateRequest(DateTimeOffset? deadline) =>
        new()
        {
            ExecutionId = "exec-1",
            StateName = "Call",
            ActionId = "statevia.action.builtin.execution.noop",
            TenantId = Guid.NewGuid().ToString("D"),
            Deadline = deadline,
        };
}

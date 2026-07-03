using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Infrastructure.Actions.Grpc.Tests;

/// <summary><see cref="ActionExecutionContractMapper"/> の双方向変換を検証する。</summary>
public sealed class ActionExecutionContractMapperTests
{
    /// <summary>リクエストの全フィールドが往復で保持される。</summary>
    [Fact]
    public void Request_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var deadline = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var original = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "SendMail",
            ActionId = "test.module.send",
            TenantId = "11111111-1111-1111-1111-111111111111",
            Input = JsonDocument.Parse("""{"ship":{"address":"Tokyo"}}""").RootElement,
            CorrelationId = "corr-9",
            Deadline = deadline,
        };

        // Act
        var rpc = ActionExecutionContractMapper.ToRpcRequest(original);
        var restored = ActionExecutionContractMapper.FromRpcRequest(rpc);

        // Assert
        Assert.Equal(original.ExecutionId, restored.ExecutionId);
        Assert.Equal(original.StateName, restored.StateName);
        Assert.Equal(original.ActionId, restored.ActionId);
        Assert.Equal(original.TenantId, restored.TenantId);
        Assert.Equal(original.CorrelationId, restored.CorrelationId);
        Assert.Equal(original.Deadline, restored.Deadline);
        Assert.Equal(
            original.Input?.GetRawText(),
            restored.Input?.GetRawText());
    }

    /// <summary>最小リクエストは省略フィールドなしで往復できる。</summary>
    [Fact]
    public void Request_RoundTrip_MinimalFields()
    {
        // Arrange
        var original = new ActionExecutionRequest
        {
            ExecutionId = "exec-min",
            StateName = "noop",
            ActionId = "statevia.builtin.noop",
            TenantId = "22222222-2222-2222-2222-222222222222",
        };

        // Act
        var restored = ActionExecutionContractMapper.FromRpcRequest(
            ActionExecutionContractMapper.ToRpcRequest(original));

        // Assert
        Assert.Null(restored.Input);
        Assert.Null(restored.CorrelationId);
        Assert.Null(restored.Deadline);
        Assert.Equal(original.ExecutionId, restored.ExecutionId);
    }

    /// <summary>成功レスポンスの output JSON が往復で保持される。</summary>
    [Fact]
    public void Response_RoundTrip_SuccessPreservesOutput()
    {
        // Arrange
        var original = new ActionExecutionResult
        {
            Success = true,
            Output = JsonDocument.Parse("""{"status":"ok"}""").RootElement,
        };

        // Act
        var restored = ActionExecutionContractMapper.FromRpcResponse(
            ActionExecutionContractMapper.ToRpcResponse(original));

        // Assert
        Assert.True(restored.Success);
        Assert.Equal(original.Output?.GetRawText(), restored.Output?.GetRawText());
        Assert.Null(restored.ErrorCode);
        Assert.Null(restored.ErrorMessage);
    }

    /// <summary>失敗レスポンスのエラー情報が往復で保持される。</summary>
    [Fact]
    public void Response_RoundTrip_FailurePreservesErrors()
    {
        // Arrange
        var original = new ActionExecutionResult
        {
            Success = false,
            ErrorCode = "HostTimeout",
            ErrorMessage = "Action Host did not respond in time.",
        };

        // Act
        var restored = ActionExecutionContractMapper.FromRpcResponse(
            ActionExecutionContractMapper.ToRpcResponse(original));

        // Assert
        Assert.False(restored.Success);
        Assert.Equal(original.ErrorCode, restored.ErrorCode);
        Assert.Equal(original.ErrorMessage, restored.ErrorMessage);
        Assert.Null(restored.Output);
    }

    /// <summary>生成された gRPC サービス型がロードできる。</summary>
    [Fact]
    public void GeneratedService_ClientTypeIsAvailable()
    {
        // Arrange
        // Act
        var clientType = typeof(ActionExecutionService.ActionExecutionServiceClient);

        // Assert
        Assert.NotNull(clientType);
    }
}

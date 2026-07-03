using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Infrastructure.Actions.Grpc;

/// <summary>
/// Phase 1 DTO と gRPC 契約メッセージの相互変換。
/// OutOfProcess 経路では <see cref="ActionExecutionResult.RuntimeOutput"/> を送受信しない。
/// </summary>
public static class ActionExecutionContractMapper
{
    /// <summary>DTO を gRPC リクエストへ変換する。</summary>
    /// <param name="request">Platform 実行リクエスト。</param>
    /// <returns>gRPC リクエスト。</returns>
    public static ActionExecutionRpcRequest ToRpcRequest(ActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rpc = new ActionExecutionRpcRequest
        {
            ExecutionId = request.ExecutionId,
            StateName = request.StateName,
            ActionId = request.ActionId,
            TenantId = request.TenantId,
        };

        if (request.Input is { } input)
            rpc.InputJson = input.GetRawText();

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
            rpc.CorrelationId = request.CorrelationId;

        if (request.Deadline is { } deadline)
            rpc.DeadlineUnixMs = deadline.ToUnixTimeMilliseconds();

        return rpc;
    }

    /// <summary>gRPC リクエストを DTO へ変換する。</summary>
    /// <param name="rpc">gRPC リクエスト。</param>
    /// <returns>Platform 実行リクエスト。</returns>
    public static ActionExecutionRequest FromRpcRequest(ActionExecutionRpcRequest rpc)
    {
        ArgumentNullException.ThrowIfNull(rpc);

        return new ActionExecutionRequest
        {
            ExecutionId = rpc.ExecutionId,
            StateName = rpc.StateName,
            ActionId = rpc.ActionId,
            TenantId = rpc.TenantId,
            Input = ParseOptionalJson(rpc.InputJson),
            CorrelationId = string.IsNullOrWhiteSpace(rpc.CorrelationId) ? null : rpc.CorrelationId,
            Deadline = rpc.HasDeadlineUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(rpc.DeadlineUnixMs)
                : null,
        };
    }

    /// <summary>DTO を gRPC レスポンスへ変換する。</summary>
    /// <param name="result">Platform 実行結果。</param>
    /// <returns>gRPC レスポンス。</returns>
    public static ActionExecutionRpcResponse ToRpcResponse(ActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var rpc = new ActionExecutionRpcResponse
        {
            Success = result.Success,
        };

        if (result.Output is { } output)
            rpc.OutputJson = output.GetRawText();

        if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            rpc.ErrorCode = result.ErrorCode;

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            rpc.ErrorMessage = result.ErrorMessage;

        return rpc;
    }

    /// <summary>gRPC レスポンスを DTO へ変換する。</summary>
    /// <param name="rpc">gRPC レスポンス。</param>
    /// <returns>Platform 実行結果。</returns>
    public static ActionExecutionResult FromRpcResponse(ActionExecutionRpcResponse rpc)
    {
        ArgumentNullException.ThrowIfNull(rpc);

        return new ActionExecutionResult
        {
            Success = rpc.Success,
            Output = ParseOptionalJson(rpc.OutputJson),
            ErrorCode = string.IsNullOrWhiteSpace(rpc.ErrorCode) ? null : rpc.ErrorCode,
            ErrorMessage = string.IsNullOrWhiteSpace(rpc.ErrorMessage) ? null : rpc.ErrorMessage,
        };
    }

    private static JsonElement? ParseOptionalJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

using Grpc.Core;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Action Host gRPC 呼び出しと結果マッピング。</summary>
internal static class ActionHostGrpcInvoker
{
    /// <summary>gRPC クライアント経由で Action を実行する。</summary>
    /// <param name="client">gRPC クライアント。</param>
    /// <param name="request">Platform 実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    public static async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionService.ActionExecutionServiceClient client,
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        return await InvokeAsync(
            (rpcRequest, deadline, ct) => client
                .ExecuteActionAsync(rpcRequest, deadline: deadline, cancellationToken: ct)
                .ResponseAsync,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>gRPC deadline の決定と例外写像をテスト可能にする。</summary>
    /// <param name="execute">RPC 実行。</param>
    /// <param name="request">Platform 実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    internal static async Task<ActionExecutionResult> InvokeAsync(
        Func<ActionExecutionRpcRequest, DateTime?, CancellationToken, Task<ActionExecutionRpcResponse>> execute,
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var rpcRequest = ActionExecutionContractMapper.ToRpcRequest(request);
            var rpcResponse = await execute(
                    rpcRequest,
                    request.Deadline?.UtcDateTime,
                    cancellationToken)
                .ConfigureAwait(false);

            return ActionExecutionContractMapper.FromRpcResponse(rpcResponse);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            return Failure("DeadlineExceeded", "Action Host execution timed out.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            return Failure("Cancelled", "Action Host execution was cancelled.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            return Failure("ActionHostUnavailable", "Action Host is unavailable.");
        }
        catch (RpcException ex)
        {
            return Failure(
                "ActionHostRpcFailed",
                $"Action Host execution failed ({ex.StatusCode}): {ex.Status.Detail}");
        }
    }

    private static ActionExecutionResult Failure(string errorCode, string message) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
        };
}

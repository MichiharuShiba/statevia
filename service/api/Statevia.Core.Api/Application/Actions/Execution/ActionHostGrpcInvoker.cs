using Grpc.Core;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Actions.Grpc;
using Statevia.Actions.Grpc.Contracts;

namespace Statevia.Core.Api.Application.Actions.Execution;

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

        try
        {
            var rpcRequest = ActionExecutionContractMapper.ToRpcRequest(request);
            var rpcResponse = await client
                .ExecuteActionAsync(rpcRequest, cancellationToken: cancellationToken)
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
        catch (RpcException)
        {
            return Failure("ActionHostRpcFailed", "Action Host execution failed.");
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

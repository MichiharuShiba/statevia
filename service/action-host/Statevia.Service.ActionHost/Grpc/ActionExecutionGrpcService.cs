using Grpc.Core;
using Statevia.Infrastructure.Actions.Grpc;
using Statevia.Infrastructure.Actions.Grpc.Contracts;
using Statevia.Service.ActionHost.Execution;

namespace Statevia.Service.ActionHost.Grpc;

/// <summary>Core-API からの OutOfProcess Action 実行 RPC。</summary>
internal sealed class ActionExecutionGrpcService : ActionExecutionService.ActionExecutionServiceBase
{
    private readonly ActionHostExecutor _executor;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="executor">Action 実行器。</param>
    public ActionExecutionGrpcService(ActionHostExecutor executor) =>
        _executor = executor;

    /// <inheritdoc />
    public override async Task<ActionExecutionRpcResponse> ExecuteAction(
        ActionExecutionRpcRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var dtoRequest = ActionExecutionContractMapper.FromRpcRequest(request);
        var result = await _executor
            .ExecuteAsync(dtoRequest, context.CancellationToken)
            .ConfigureAwait(false);

        return ActionExecutionContractMapper.ToRpcResponse(result);
    }
}

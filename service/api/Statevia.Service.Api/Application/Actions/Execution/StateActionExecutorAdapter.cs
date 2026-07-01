using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary><see cref="IStateExecutor"/> から <see cref="IActionExecutor"/> への Adapter。</summary>
internal sealed class StateActionExecutorAdapter : IStateExecutor
{
    private readonly string _actionId;
    private readonly string _tenantId;
    private readonly IActionExecutor _actionExecutor;

    /// <summary>
    /// Adapter を構築する。
    /// </summary>
    /// <param name="actionId">canonical actionId。</param>
    /// <param name="tenantId"><c>tenants.tenant_id</c> UUID 文字列。</param>
    /// <param name="actionExecutor">Platform 実行ディスパッチャ。</param>
    public StateActionExecutorAdapter(string actionId, string tenantId, IActionExecutor actionExecutor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(actionExecutor);
        _actionId = actionId;
        _tenantId = tenantId;
        _actionExecutor = actionExecutor;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        var request = new ActionExecutionRequest
        {
            ExecutionId = ctx.ExecutionId,
            StateName = ctx.StateName,
            ActionId = _actionId,
            TenantId = _tenantId,
        };

        var result = await _actionExecutor
            .ExecuteAsync(request, ctx, input, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? result.ErrorCode ?? "Action execution failed.");
        }

        return ActionExecutionRuntimeInputMapper.ToRuntimeOutput(result);
    }
}

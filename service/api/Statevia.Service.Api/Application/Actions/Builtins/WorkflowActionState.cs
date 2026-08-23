using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Application.Actions.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>子ワークフローを起動する Workflow capability。</summary>
/// <remarks>
/// 親実行 ID は <see cref="StateContext.ExecutionId"/> から取る。子 Start は親 security snapshot を継承する。
/// 開始は常に非同期（開始直後の status を返す）。子の終端待ちは Wait / Resume で行う。
/// </remarks>
internal sealed class WorkflowActionState : IState<object?, object?>
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>スコープ付き runner 解決用に構築する。</summary>
    public WorkflowActionState(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("workflow action requires input.definitionId.");
        }

        var definitionId = ActionInputReader.RequireString(fields, "definitionId");
        object? childInput = fields.TryGetValue("input", out var inputElement)
            ? inputElement
            : null;

        if (!Guid.TryParse(ctx.ExecutionId, out var parentExecutionId) || parentExecutionId == Guid.Empty)
        {
            throw new ArgumentException("workflow action requires a parent execution id.");
        }

        using var scope = _scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IChildWorkflowRunner>();
        var result = await runner
            .RunAsync(new ChildWorkflowRequest(definitionId, childInput, parentExecutionId), ct)
            .ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["workflowId"] = result.WorkflowId,
            ["displayId"] = result.DisplayId,
            ["status"] = result.Status,
        };
    }
}

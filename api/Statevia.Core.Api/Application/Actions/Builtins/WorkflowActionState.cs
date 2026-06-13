using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Api.Application.Actions.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>子ワークフローを起動する Workflow capability。</summary>
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
            throw new ArgumentException("workflow action requires input.definitionId and input.mode.");
        }

        var definitionId = ActionInputReader.RequireString(fields, "definitionId");
        var mode = ActionInputReader.RequireString(fields, "mode");
        if (mode is not ("async" or "sync"))
        {
            throw new ArgumentException("workflow action mode must be async or sync.");
        }

        TimeSpan? timeout = null;
        if (fields.TryGetValue("timeout", out var timeoutElement)
            && timeoutElement.TryGetInt32(out var timeoutSeconds))
        {
            timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        object? childInput = fields.TryGetValue("input", out var inputElement)
            ? inputElement
            : null;

        using var scope = _scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IChildWorkflowRunner>();
        var result = await runner
            .RunAsync(new ChildWorkflowRequest(definitionId, mode, childInput, timeout), ct)
            .ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["workflowId"] = result.WorkflowId,
            ["displayId"] = result.DisplayId,
            ["status"] = result.Status,
        };
    }
}

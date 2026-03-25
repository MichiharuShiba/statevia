using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Builtins;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>状態定義の wait / action に応じて <see cref="IStateExecutor"/> を解決する（引擎は状態名のみ参照）。</summary>
public sealed class ActionExecutorFactory : IStateExecutorFactory
{
    private readonly WorkflowDefinition _definition;
    private readonly IActionRegistry _registry;

    public ActionExecutorFactory(WorkflowDefinition definition, IActionRegistry registry)
    {
        _definition = definition;
        _registry = registry;
    }

    public IStateExecutor? GetExecutor(string stateName)
    {
        if (!_definition.States.TryGetValue(stateName, out var state))
            return null;

        if (state.Wait != null)
            return DefaultStateExecutor.Create(new WaitOnlyState(state.Wait.Event));

        var actionId = string.IsNullOrWhiteSpace(state.Action) ? WellKnownActionIds.NoOp : state.Action.Trim();
        return _registry.TryResolve(actionId, out var executor) ? executor : null;
    }
}

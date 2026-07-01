using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>状態定義の wait / action に応じて <see cref="IStateExecutor"/> を解決する（引擎は状態名のみ参照）。</summary>
internal sealed class ActionExecutorFactory : IStateExecutorFactory
{
    private readonly WorkflowDefinition _definition;
    private readonly IActionCatalog _catalog;
    private readonly IActionExecutor _actionExecutor;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>
    /// 定義と Catalog から状態実行器ファクトリを構築する。
    /// </summary>
    /// <param name="definition">コンパイル対象のワークフロー定義。</param>
    /// <param name="catalog">Action Catalog。</param>
    /// <param name="serviceProvider">Platform 実行層とテナント文脈の解決用。</param>
    public ActionExecutorFactory(
        WorkflowDefinition definition,
        IActionCatalog catalog,
        IServiceProvider serviceProvider)
    {
        _definition = definition;
        _catalog = catalog;
        _actionExecutor = serviceProvider.GetRequiredService<IActionExecutor>();
        _tenantContext = serviceProvider.GetRequiredService<ITenantContextAccessor>();
    }

    /// <inheritdoc />
    public IStateExecutor? GetExecutor(string stateName)
    {
        if (!_definition.States.TryGetValue(stateName, out var state))
        {
            return null;
        }

        if (state.Wait is not null)
        {
            return DefaultStateExecutor.Create(new WaitOnlyState(state.Wait.Event));
        }

        var actionId = string.IsNullOrWhiteSpace(state.Action)
            ? WellKnownActionIds.NoOpCanonical
            : state.Action.Trim();

        if (!_catalog.Exists(actionId))
        {
            return null;
        }

        var tenantId = _tenantContext.TenantId?.ToString("D")
            ?? throw new InvalidOperationException("Tenant context is required for action execution.");

        return new StateActionExecutorAdapter(actionId, tenantId, _actionExecutor);
    }
}

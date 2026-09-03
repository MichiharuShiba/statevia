using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>状態定義の wait（events / subscribe）または action に応じて <see cref="IStateExecutor"/> を解決する（引擎は状態名のみ参照）。</summary>
internal sealed class ActionExecutorFactory : IStateExecutorFactory
{
    private readonly WorkflowDefinition _definition;
    private readonly IReadOnlyDictionary<string, StateActionBinding> _stateActionBindings;
    private readonly IActionCatalog _catalog;
    private readonly IActionExecutor _actionExecutor;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>
    /// 定義と Catalog から状態実行器ファクトリを構築する。
    /// </summary>
    /// <param name="definition">コンパイル対象のワークフロー定義。</param>
    /// <param name="stateActionBindings">状態別 Action バインディング（版ピン付き）。</param>
    /// <param name="catalog">Action Catalog。</param>
    /// <param name="serviceProvider">Platform 実行層とテナント文脈の解決用。</param>
    public ActionExecutorFactory(
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, StateActionBinding> stateActionBindings,
        IActionCatalog catalog,
        IServiceProvider serviceProvider)
    {
        _definition = definition;
        _stateActionBindings = stateActionBindings;
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
            var eventNames = ResolveWaitEventNames(stateName, state.Wait);
            return DefaultStateExecutor.Create(new WaitOnlyState(eventNames));
        }

        if (!_stateActionBindings.TryGetValue(stateName, out var binding))
        {
            return null;
        }

        if (!CatalogRegistrationExists(binding))
        {
            return null;
        }

        var tenantId = _tenantContext.TenantId?.ToString("D")
            ?? throw new InvalidOperationException("Tenant context is required for action execution.");

        return new StateActionExecutorAdapter(
            binding.LogicalActionId,
            binding.ResolvedModuleVersion,
            tenantId,
            _actionExecutor);
    }

    /// <summary>
    /// Wait が待つイベント名を解決する。Subscribe は内部 resume 名、Signal は <c>events</c> キー。
    /// </summary>
    /// <param name="stateName">状態名（例外メッセージ用）。</param>
    /// <param name="wait">Wait 定義。</param>
    /// <returns>非空のイベント名一覧。</returns>
    /// <exception cref="InvalidOperationException">events / subscribe のどちらも空のとき。</exception>
    private static string[] ResolveWaitEventNames(string stateName, WaitDefinition wait)
    {
        if (wait.Subscribe.Count > 0)
        {
            return wait.Subscribe
                .Select((_, index) => WaitSubscribeEventNames.ForIndex(index))
                .ToArray();
        }

        var eventNames = wait.Events.Keys.ToArray();
        if (eventNames.Length == 0)
        {
            throw new InvalidOperationException($"Wait state '{stateName}' has no events.");
        }

        return eventNames;
    }

    private bool CatalogRegistrationExists(StateActionBinding binding)
    {
        if (binding.ResolvedModuleVersion is { Length: > 0 } version
            && binding.ModuleId is { Length: > 0 } moduleId
            && binding.ActionName is { Length: > 0 } actionName)
        {
            return _catalog.TryGetRegistration(moduleId, version, actionName, out _);
        }

        return _catalog.Exists(binding.LogicalActionId);
    }
}

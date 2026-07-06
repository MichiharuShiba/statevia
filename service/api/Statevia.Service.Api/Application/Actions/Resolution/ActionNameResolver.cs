using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Application.Actions.Resolution;

/// <summary>
/// Compiler フェーズで action 参照を canonical ID に正規化する。
/// Loader は未正規化の生値を保持し、本クラスが Builtin 短名 / module alias / FQCN 解決を担う。
/// </summary>
internal static class ActionNameResolver
{
    /// <summary>action 参照を解決した新しい <see cref="WorkflowDefinition"/> を返す。</summary>
    /// <param name="definition">syntax parse 済みの定義。</param>
    /// <returns>各状態の <see cref="StateDefinition.Action"/> が正規化された定義。</returns>
    public static WorkflowDefinition Resolve(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var modules = definition.Modules ?? EmptyModules;
        var resolvedStates = definition.States.ToDictionary(
            entry => entry.Key,
            entry => ResolveState(entry.Value, modules),
            StringComparer.OrdinalIgnoreCase);

        return new WorkflowDefinition
        {
            Name = definition.Name,
            Modules = definition.Modules,
            States = resolvedStates,
        };
    }

    private static readonly IReadOnlyDictionary<string, ModuleImportReference> EmptyModules =
        new Dictionary<string, ModuleImportReference>(StringComparer.OrdinalIgnoreCase);

    private static StateDefinition ResolveState(
        StateDefinition state,
        IReadOnlyDictionary<string, ModuleImportReference> modules)
    {
        if (state.Wait is not null || state.Join is not null)
        {
            return state;
        }

        var resolvedAction = string.IsNullOrWhiteSpace(state.Action)
            ? WellKnownActionIds.NoOpCanonical
            : ResolveActionRef(state.Action.Trim(), modules);

        return new StateDefinition
        {
            Action = resolvedAction,
            On = state.On,
            Wait = state.Wait,
            Join = state.Join,
            Input = state.Input,
            Retry = state.Retry,
        };
    }

    private static string ResolveActionRef(string actionRef, IReadOnlyDictionary<string, ModuleImportReference> modules)
    {
        if (WellKnownActionIds.IsBuiltinShortName(actionRef))
        {
            return WellKnownActionIds.ToCanonicalActionId(actionRef);
        }

        var dotIndex = actionRef.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex >= 0)
        {
            var alias = actionRef[..dotIndex];
            var actionName = actionRef[(dotIndex + 1)..];

            if (string.IsNullOrWhiteSpace(actionName))
            {
                throw new ArgumentException(
                    $"Invalid action '{actionRef}': action name is required after module alias.");
            }

            if (modules.TryGetValue(alias, out var moduleImport))
            {
                return $"{moduleImport.ModuleId}.{actionName}";
            }

            if (!actionRef.AsSpan(dotIndex + 1).Contains('.'))
            {
                throw new ArgumentException(
                    $"Unknown module alias '{alias}' in action '{actionRef}'.");
            }

            return actionRef;
        }

        throw new ArgumentException(
            $"Unknown action '{actionRef}': not a builtin short name, moduleAlias.actionName, or FQCN.");
    }
}

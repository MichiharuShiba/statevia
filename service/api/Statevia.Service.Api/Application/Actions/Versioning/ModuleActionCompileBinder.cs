using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Engine.Definition;
using Statevia.Service.Api.Application.Actions;

namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>compile 時の Module 版解決と状態別 Action バインディングを構築する。</summary>
internal static class ModuleActionCompileBinder
{
    /// <summary>compile 結果（確定 Module import と状態別バインディング）。</summary>
    /// <param name="ResolvedModules">alias → 確定版。</param>
    /// <param name="StateActionBindings">stateName → バインディング。</param>
    internal sealed record Result(
        IReadOnlyDictionary<string, ResolvedModuleBinding> ResolvedModules,
        IReadOnlyDictionary<string, StateActionBinding> StateActionBindings);

    /// <summary>ワークフロー定義と Catalog から compile バインディングを構築する。</summary>
    /// <param name="definition">syntax parse 済み定義（Action は未正規化でも可）。</param>
    /// <param name="catalog">ロード済み Module 版の参照元。</param>
    /// <returns>確定 import と状態別バインディング。</returns>
    public static Result Bind(WorkflowDefinition definition, IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(catalog);

        var modules = definition.Modules ?? EmptyModules;
        var resolvedModules = ResolveModuleImports(modules, catalog);
        var stateBindings = BuildStateBindings(definition, modules, resolvedModules, catalog);

        return new Result(resolvedModules, stateBindings);
    }

    private static readonly IReadOnlyDictionary<string, ModuleImportReference> EmptyModules =
        new Dictionary<string, ModuleImportReference>(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, ResolvedModuleBinding> ResolveModuleImports(
        IReadOnlyDictionary<string, ModuleImportReference> modules,
        IActionCatalog catalog)
    {
        var resolved = new Dictionary<string, ResolvedModuleBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var (alias, import) in modules)
        {
            var reference = ModuleImportParser.Parse(alias, import);
            var loadedVersionStrings = catalog.GetLoadedVersions(reference.ModuleId);
            if (loadedVersionStrings.Count == 0)
            {
                throw new ModuleVersionResolutionException(
                    $"Module '{reference.ModuleId}' (alias '{alias}') has no loaded versions.");
            }

            var loadedVersions = loadedVersionStrings
                .Select(ModuleVersion.Parse)
                .ToList();

            var resolvedReference = ModuleVersionResolver.Resolve(reference, loadedVersions);
            resolved[alias] = new ResolvedModuleBinding(
                resolvedReference.ModuleId,
                resolvedReference.ResolvedVersion);
        }

        return resolved;
    }

    private static Dictionary<string, StateActionBinding> BuildStateBindings(
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, ModuleImportReference> modules,
        IReadOnlyDictionary<string, ResolvedModuleBinding> resolvedModules,
        IActionCatalog catalog)
    {
        var bindings = new Dictionary<string, StateActionBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var (stateName, state) in definition.States)
        {
            if (state.Wait is not null || state.Join is not null)
            {
                continue;
            }

            var actionRef = string.IsNullOrWhiteSpace(state.Action)
                ? WellKnownActionIds.NoOpCanonical
                : state.Action.Trim();

            bindings[stateName] = ResolveStateBinding(actionRef, modules, resolvedModules, catalog);
        }

        return bindings;
    }

    private static StateActionBinding ResolveStateBinding(
        string actionRef,
        IReadOnlyDictionary<string, ModuleImportReference> modules,
        IReadOnlyDictionary<string, ResolvedModuleBinding> resolvedModules,
        IActionCatalog catalog)
    {
        if (WellKnownActionIds.IsBuiltinShortName(actionRef))
        {
            var logical = WellKnownActionIds.ToCanonicalActionId(actionRef);
            return new StateActionBinding(logical, ResolvedModuleVersion: null);
        }

        if (string.Equals(actionRef, WellKnownActionIds.NoOpCanonical, StringComparison.Ordinal))
        {
            return new StateActionBinding(WellKnownActionIds.NoOpCanonical, ResolvedModuleVersion: null);
        }

        var dotIndex = actionRef.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex < 0)
        {
            throw new ArgumentException(
                $"Unknown action '{actionRef}': not a builtin short name, moduleAlias.actionName, or FQCN.");
        }

        var prefix = actionRef[..dotIndex];
        var actionName = actionRef[(dotIndex + 1)..];
        if (string.IsNullOrWhiteSpace(actionName))
        {
            throw new ArgumentException(
                $"Invalid action '{actionRef}': action name is required after module alias.");
        }

        if (modules.ContainsKey(prefix))
        {
            if (!resolvedModules.TryGetValue(prefix, out var resolvedModule))
            {
                throw new ModuleVersionResolutionException(
                    $"Module alias '{prefix}' is not resolved.");
            }

            var logicalActionId = $"{resolvedModule.ModuleId}.{actionName}";
            return new StateActionBinding(
                logicalActionId,
                resolvedModule.ResolvedVersion,
                resolvedModule.ModuleId,
                actionName);
        }

        if (!actionRef.AsSpan(dotIndex + 1).Contains('.'))
        {
            throw new ArgumentException(
                $"Unknown module alias '{prefix}' in action '{actionRef}'.");
        }

        // FQCN: moduleId は最後のセグメント手前までとみなし、Legacy 単一版のみ許可。
        var lastDot = actionRef.LastIndexOf('.');
        var moduleId = actionRef[..lastDot];
        var fqcnActionName = actionRef[(lastDot + 1)..];
        var fqcnVersions = catalog.GetLoadedVersions(moduleId);
        if (fqcnVersions.Count > 1)
        {
            throw new DefinitionMigrationRequiredException(
                $"Module '{moduleId}' has multiple loaded versions; recompile the workflow with workflow.modules alias imports.");
        }

        if (fqcnVersions.Count == 1)
        {
            return new StateActionBinding(
                actionRef,
                fqcnVersions[0],
                moduleId,
                fqcnActionName);
        }

        return new StateActionBinding(actionRef, ResolvedModuleVersion: null);
    }
}

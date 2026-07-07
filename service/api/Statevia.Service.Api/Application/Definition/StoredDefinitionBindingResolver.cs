using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>保存済み compiled JSON から版バインディングを復元する（Legacy フォールバック含む）。</summary>
internal static class StoredDefinitionBindingResolver
{
    /// <summary>compiled JSON または Legacy 経路から版バインディングを解決する。</summary>
    /// <param name="definition">syntax parse 済み定義（action 正規化済み）。</param>
    /// <param name="compiledJson">保存済み compiled JSON。</param>
    /// <param name="catalog">ロード済み Module 版の参照元。</param>
    /// <returns>確定 import と状態別バインディング。</returns>
    public static ModuleActionCompileBinder.Result Resolve(
        WorkflowDefinition definition,
        string compiledJson,
        IActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledJson);
        ArgumentNullException.ThrowIfNull(catalog);

        if (CompiledDefinitionJsonReader.HasStoredBindings(compiledJson))
        {
            var (resolvedModules, stateActionBindings) = CompiledDefinitionJsonReader.ReadStoredBindings(compiledJson);
            ValidatePinnedVersionsLoaded(stateActionBindings, catalog);
            return new ModuleActionCompileBinder.Result(resolvedModules, stateActionBindings);
        }

        EnsureLegacyRestorable(definition, catalog);
        return ModuleActionCompileBinder.Bind(definition, catalog);
    }

    private static void ValidatePinnedVersionsLoaded(
        IReadOnlyDictionary<string, StateActionBinding> bindings,
        IActionCatalog catalog)
    {
        foreach (var binding in bindings.Values)
        {
            if (binding.ResolvedModuleVersion is not { Length: > 0 } version
                || binding.ModuleId is not { Length: > 0 } moduleId
                || binding.ActionName is not { Length: > 0 } actionName)
            {
                continue;
            }

            if (catalog.TryGetDescriptor(moduleId, version, actionName, out _))
            {
                continue;
            }

            throw new DefinitionMigrationRequiredException(
                $"Pinned module version '{moduleId}@{version}' is not loaded. Install the module version or recompile the workflow.");
        }
    }

    private static void EnsureLegacyRestorable(WorkflowDefinition definition, IActionCatalog catalog)
    {
        if (definition.Modules is not null)
        {
            foreach (var import in definition.Modules.Values)
            {
                EnsureSingleLoadedVersion(import.ModuleId, catalog);
            }
        }

        foreach (var state in definition.States.Values)
        {
            if (!state.IsActionResolvable || string.IsNullOrWhiteSpace(state.Action))
            {
                continue;
            }

            if (!TryGetFqcnModuleId(state.Action.Trim(), definition.Modules, out var moduleId))
            {
                continue;
            }

            EnsureSingleLoadedVersion(moduleId, catalog);
        }
    }

    private static void EnsureSingleLoadedVersion(string moduleId, IActionCatalog catalog)
    {
        if (catalog.GetLoadedVersions(moduleId).Count <= 1)
        {
            return;
        }

        throw new DefinitionMigrationRequiredException(
            $"Module '{moduleId}' has multiple loaded versions; recompile the workflow with workflow.modules alias imports.");
    }

    private static bool TryGetFqcnModuleId(
        string actionRef,
        IReadOnlyDictionary<string, ModuleImportReference>? modules,
        out string moduleId)
    {
        moduleId = string.Empty;
        var dotIndex = actionRef.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex < 0)
        {
            return false;
        }

        var prefix = actionRef[..dotIndex];
        if (modules is not null && modules.ContainsKey(prefix))
        {
            return false;
        }

        if (!actionRef.AsSpan(dotIndex + 1).Contains('.'))
        {
            return false;
        }

        var lastDot = actionRef.LastIndexOf('.');
        moduleId = actionRef[..lastDot];
        return moduleId.Length > 0;
    }
}

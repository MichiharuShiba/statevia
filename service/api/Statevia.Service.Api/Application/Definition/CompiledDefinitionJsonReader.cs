using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>definition_versions.compiled_json から Engine 投入用定義を復元する。</summary>
internal static class CompiledDefinitionJsonReader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>保存済み compiled_json に版バインディングが含まれるか。</summary>
    public static bool HasStoredBindings(string compiledJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledJson);

        var dto = Deserialize(compiledJson);
        return dto.StateActionBindings is { Count: > 0 };
    }

    /// <summary>保存済み compiled_json から版バインディングを読み取る。</summary>
    public static (
        IReadOnlyDictionary<string, ResolvedModuleBinding> ResolvedModules,
        IReadOnlyDictionary<string, StateActionBinding> StateActionBindings) ReadStoredBindings(string compiledJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledJson);

        var dto = Deserialize(compiledJson);
        return (MapResolvedModules(dto.ResolvedModules), MapStateActionBindings(dto.StateActionBindings));
    }

    /// <summary>保存済み compiled_json と factory から実行定義を復元する。</summary>
    /// <param name="compiledJson">definition_versions.compiled_json。</param>
    /// <param name="factory">状態実行器ファクトリ。</param>
    /// <param name="resolvedModules">版ピン済み module 解決。省略時は JSON から復元。</param>
    /// <param name="stateActionBindings">状態別 Action バインディング。省略時は JSON から復元。</param>
    /// <param name="waitSubscriptions">
    /// 明示購読表。指定時は JSON より優先する（YAML からの再構築。古い compiled_json 欠落の補完）。
    /// </param>
    public static CompiledWorkflowDefinition Read(
        string compiledJson,
        IStateExecutorFactory factory,
        IReadOnlyDictionary<string, ResolvedModuleBinding>? resolvedModules = null,
        IReadOnlyDictionary<string, StateActionBinding>? stateActionBindings = null,
        IReadOnlyDictionary<string, IReadOnlyList<WaitSubscriptionDefinition>>? waitSubscriptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledJson);
        ArgumentNullException.ThrowIfNull(factory);

        var dto = Deserialize(compiledJson);
        var modules = resolvedModules ?? MapResolvedModules(dto.ResolvedModules);
        var bindings = stateActionBindings ?? MapStateActionBindings(dto.StateActionBindings);

        return new CompiledWorkflowDefinition
        {
            Name = dto.Name,
            InitialState = dto.InitialState,
            Transitions = MapTransitions(dto.Transitions),
            ConditionalTransitions = MapConditionalTransitions(dto.ConditionalTransitions),
            ForkTable = MapStringListTable(dto.ForkTable),
            JoinTable = MapStringListTable(dto.JoinTable),
            WaitEventRouteTable = MapWaitEventRouteTable(dto.WaitEventRouteTable),
            WaitSubscriptions = waitSubscriptions ?? MapWaitSubscriptions(dto.WaitSubscriptions),
            StateInputs = dto.StateInputs ?? [],
            StateOutputs = dto.StateOutputs ?? [],
            ResolvedModules = modules,
            StateActionBindings = bindings,
            StateExecutorFactory = factory
        };
    }

    private static CompiledDefinitionDto Deserialize(string compiledJson) =>
        JsonSerializer.Deserialize<CompiledDefinitionDto>(compiledJson, s_options)
        ?? throw new ArgumentException("compiled_json is empty or invalid.");

    private static Dictionary<string, ResolvedModuleBinding> MapResolvedModules(
        Dictionary<string, ResolvedModuleBindingDto>? resolvedModules)
    {
        if (resolvedModules is null || resolvedModules.Count == 0)
        {
            return new Dictionary<string, ResolvedModuleBinding>(StringComparer.OrdinalIgnoreCase);
        }

        return resolvedModules.ToDictionary(
            pair => pair.Key,
            pair => new ResolvedModuleBinding(pair.Value.ModuleId, pair.Value.ResolvedVersion),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, StateActionBinding> MapStateActionBindings(
        Dictionary<string, StateActionBindingDto>? stateActionBindings)
    {
        if (stateActionBindings is null || stateActionBindings.Count == 0)
        {
            return new Dictionary<string, StateActionBinding>(StringComparer.OrdinalIgnoreCase);
        }

        return stateActionBindings.ToDictionary(
            pair => pair.Key,
            pair => new StateActionBinding(
                pair.Value.LogicalActionId,
                pair.Value.ResolvedModuleVersion,
                pair.Value.ModuleId,
                pair.Value.ActionName),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> MapTransitions(
        Dictionary<string, Dictionary<string, TransitionTargetDto>?>? transitions)
    {
        if (transitions is null)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase);
        }

        return transitions.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, TransitionTarget>)(
                pair.Value ?? [])
                .ToDictionary(
                    inner => inner.Key,
                    inner => MapTarget(inner.Value),
                    StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>> MapConditionalTransitions(
        Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? conditionalTransitions)
    {
        if (conditionalTransitions is null)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>(StringComparer.OrdinalIgnoreCase);
        }

        return conditionalTransitions.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, CompiledFactTransition>)(
                pair.Value ?? [])
                .ToDictionary(
                    inner => inner.Key,
                    inner => MapConditionalTransition(inner.Value),
                    StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static CompiledFactTransition MapConditionalTransition(CompiledFactTransitionDto dto) =>
        new()
        {
            LinearTarget = dto.LinearTarget is null ? null : MapTarget(dto.LinearTarget),
            DefaultTarget = dto.DefaultTarget is null ? null : MapTarget(dto.DefaultTarget),
            Cases = dto.Cases?
                .Where(c => c is not null)
                .Select(c => new CompiledTransitionCase
                {
                    Order = c!.Order,
                    DeclarationIndex = c.DeclarationIndex,
                    When = c.When ?? throw new ArgumentException("conditional transition case is missing when."),
                    Target = c.Target is null
                        ? throw new ArgumentException("conditional transition case is missing target.")
                        : MapTarget(c.Target)
                })
                .ToList() ?? []
        };

    private static Dictionary<string, IReadOnlyList<string>> MapStringListTable(
        Dictionary<string, List<string>?>? table)
    {
        if (table is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return table.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)(pair.Value ?? []),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>> MapWaitEventRouteTable(
        Dictionary<string, Dictionary<string, WaitEventRouteDto>?>? table)
    {
        if (table is null)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
                StringComparer.OrdinalIgnoreCase);
        }

        return table.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, WaitEventRouteDefinition>)(
                pair.Value ?? [])
                .ToDictionary(
                    inner => inner.Key,
                    inner => new WaitEventRouteDefinition
                    {
                        Next = inner.Value?.Next
                            ?? throw new ArgumentException(
                                $"waitEventRouteTable['{pair.Key}']['{inner.Key}'] is missing next.")
                    },
                    StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>compiled_json の waitSubscriptions を Engine 型へ写像する。</summary>
    private static Dictionary<string, IReadOnlyList<WaitSubscriptionDefinition>> MapWaitSubscriptions(
        Dictionary<string, List<WaitSubscriptionDto>?>? table)
    {
        if (table is null || table.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<WaitSubscriptionDefinition>>(
                StringComparer.OrdinalIgnoreCase);
        }

        return table.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<WaitSubscriptionDefinition>)(pair.Value ?? [])
                .Select((entry, index) => MapWaitSubscription(pair.Key, index, entry))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static WaitSubscriptionDefinition MapWaitSubscription(
        string stateName,
        int index,
        WaitSubscriptionDto? entry)
    {
        if (entry is null)
        {
            throw new ArgumentException($"waitSubscriptions['{stateName}'][{index}] is missing.");
        }

        if (string.IsNullOrWhiteSpace(entry.Topic))
        {
            throw new ArgumentException($"waitSubscriptions['{stateName}'][{index}] is missing topic.");
        }

        if (string.IsNullOrWhiteSpace(entry.ResumeEventName))
        {
            throw new ArgumentException($"waitSubscriptions['{stateName}'][{index}] is missing resumeEventName.");
        }

        if (string.IsNullOrWhiteSpace(entry.Next))
        {
            throw new ArgumentException($"waitSubscriptions['{stateName}'][{index}] is missing next.");
        }

        return new WaitSubscriptionDefinition
        {
            Topic = entry.Topic.Trim(),
            Key = string.IsNullOrWhiteSpace(entry.Key) ? string.Empty : entry.Key.Trim(),
            ResumeEventName = entry.ResumeEventName.Trim(),
            Next = entry.Next.Trim()
        };
    }

    private static TransitionTarget MapTarget(TransitionTargetDto dto) =>
        new()
        {
            Next = dto.Next,
            Fork = dto.Fork,
            End = dto.End
        };

    private sealed class CompiledDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string InitialState { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, TransitionTargetDto>?>? Transitions { get; set; } = [];
        public Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? ConditionalTransitions { get; set; } = [];
        public Dictionary<string, List<string>?>? ForkTable { get; set; } = [];
        public Dictionary<string, List<string>?>? JoinTable { get; set; } = [];
        public Dictionary<string, Dictionary<string, WaitEventRouteDto>?>? WaitEventRouteTable { get; set; } = [];
        public Dictionary<string, List<WaitSubscriptionDto>?>? WaitSubscriptions { get; set; } = [];
        public Dictionary<string, StateInputDefinition>? StateInputs { get; set; } = [];
        public Dictionary<string, string>? StateOutputs { get; set; } = [];
        public Dictionary<string, ResolvedModuleBindingDto>? ResolvedModules { get; set; } = [];
        public Dictionary<string, StateActionBindingDto>? StateActionBindings { get; set; } = [];
    }

    private sealed class WaitEventRouteDto
    {
        public string Next { get; set; } = string.Empty;
    }

    private sealed class WaitSubscriptionDto
    {
        public string Topic { get; set; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public string ResumeEventName { get; set; } = string.Empty;
        public string Next { get; set; } = string.Empty;
    }

    private sealed class ResolvedModuleBindingDto
    {
        public string ModuleId { get; set; } = string.Empty;
        public string ResolvedVersion { get; set; } = string.Empty;
    }

    private sealed class StateActionBindingDto
    {
        public string LogicalActionId { get; set; } = string.Empty;

        [SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public string? ResolvedModuleVersion { get; set; }

        [SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public string? ModuleId { get; set; }

        [SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public string? ActionName { get; set; }
    }

    private sealed class CompiledFactTransitionDto
    {
        public TransitionTargetDto? LinearTarget { get; set; } = new();
        public List<CompiledTransitionCaseDto?>? Cases { get; set; } = [];
        public TransitionTargetDto? DefaultTarget { get; set; } = new();
    }

    private sealed class CompiledTransitionCaseDto
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public int? Order { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public int DeclarationIndex { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public ConditionExpressionDefinition? When { get; set; }

        public TransitionTargetDto? Target { get; set; } = new();
    }

    private sealed class TransitionTargetDto
    {
        public string? Next { get; set; } = string.Empty;
        public List<string>? Fork { get; set; } = [];
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "JSON 逆シリアル化が setter を使用する。")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Minor Code Smell",
            "S3459:Unassigned members should be set or removed",
            Justification = "JSON 逆シリアル化で代入される。")]
        public bool End { get; set; }
    }
}

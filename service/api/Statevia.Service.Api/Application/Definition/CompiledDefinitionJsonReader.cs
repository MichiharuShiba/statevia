using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>definition_versions.compiled_json から Engine 投入用定義を復元する。</summary>
internal static class CompiledDefinitionJsonReader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>保存済み compiled_json と factory から実行定義を復元する。</summary>
    public static CompiledWorkflowDefinition Read(string compiledJson, IStateExecutorFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledJson);
        ArgumentNullException.ThrowIfNull(factory);

        var dto = JsonSerializer.Deserialize<CompiledDefinitionDto>(compiledJson, s_options)
            ?? throw new ArgumentException("compiled_json is empty or invalid.");

        return new CompiledWorkflowDefinition
        {
            Name = dto.Name,
            InitialState = dto.InitialState,
            Transitions = MapTransitions(dto.Transitions),
            ConditionalTransitions = MapConditionalTransitions(dto.ConditionalTransitions),
            ForkTable = MapStringListTable(dto.ForkTable),
            JoinTable = MapStringListTable(dto.JoinTable),
            WaitTable = dto.WaitTable ?? [],
            StateInputs = dto.StateInputs ?? [],
            StateExecutorFactory = factory
        };
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
        public Dictionary<string, string>? WaitTable { get; set; } = [];
        public Dictionary<string, StateInputDefinition>? StateInputs { get; set; } = [];
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

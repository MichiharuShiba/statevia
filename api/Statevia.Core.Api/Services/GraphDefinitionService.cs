using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>定義の compiled_json から契約の Graph Definition（nodes / edges）を組み立てる。</summary>
internal sealed class GraphDefinitionService : IGraphDefinitionService
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IDisplayIdService _displayIds;

    public GraphDefinitionService(
        IDbContextFactory<CoreDbContext> dbFactory,
        IDisplayIdService displayIds)
    {
        _dbFactory = dbFactory;
        _displayIds = displayIds;
    }

    public async Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, string tenantId, CancellationToken ct = default)
    {
        var uuid = await _displayIds.ResolveAsync("definition", graphId, ct).ConfigureAwait(false);
        if (uuid == null)
            throw new NotFoundException("Graph not found");

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == uuid && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException("Graph not found");

        return BuildFromCompiledJson(graphId, row.Name, row.CompiledJson);
    }

    internal static GraphDefinitionResponse BuildFromCompiledJson(string graphId, string definitionName, string compiledJson)
    {
        var dto = DeserializeCompiledDefinition(compiledJson);
        if (dto is null)
            return EmptyGraph(graphId);

        var stateNames = CollectStateNames(dto);
        var nodes = BuildNodes(stateNames, dto);
        var edges = BuildEdges(dto);

        return new GraphDefinitionResponse
        {
            GraphId = graphId,
            Nodes = nodes,
            Edges = edges
        };
    }

    /// <summary>
    /// コンパイル済み定義 JSON を DTO に逆シリアライズする。
    /// </summary>
    private static CompiledDefinitionDto? DeserializeCompiledDefinition(string compiledJson)
    {
        return JsonSerializer.Deserialize<CompiledDefinitionDto>(compiledJson, CaseInsensitiveJsonSerializerOptions);
    }

    /// <summary>
    /// ノード・エッジが空の GraphDefinitionResponse を生成する。
    /// </summary>
    private static GraphDefinitionResponse EmptyGraph(string graphId) =>
        new() { GraphId = graphId, Nodes = Array.Empty<GraphNodeDefinition>(), Edges = Array.Empty<GraphEdgeDefinition>() };

    /// <summary>
    /// compiled 定義全体から出現しうる状態名を収集する。
    /// </summary>
    private static HashSet<string> CollectStateNames(CompiledDefinitionDto dto)
    {
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { dto.InitialState };
        AddStatesFromTransitions(stateNames, dto.Transitions);
        AddStatesFromConditionalTransitions(stateNames, dto.ConditionalTransitions);
        AddStatesFromStateTable(stateNames, dto.ForkTable);
        AddStatesFromStateTable(stateNames, dto.JoinTable);
        AddStatesFromWaitTable(stateNames, dto.WaitTable);
        return stateNames;
    }

    /// <summary>
    /// 収集済み状態名から描画用ノード定義を構築する。
    /// </summary>
    private static List<GraphNodeDefinition> BuildNodes(IEnumerable<string> stateNames, CompiledDefinitionDto dto) =>
        stateNames.Select(state => new GraphNodeDefinition
        {
            NodeId = state,
            NodeType = GetNodeType(state, dto),
            Label = state
        }).ToList();

    /// <summary>
    /// compiled 定義から描画用エッジ定義を構築する。
    /// </summary>
    private static List<GraphEdgeDefinition> BuildEdges(CompiledDefinitionDto dto)
    {
        var edges = new List<GraphEdgeDefinition>();
        AddEdgesFromTransitions(edges, dto.Transitions);
        AddEdgesFromConditionalTransitions(edges, dto.ConditionalTransitions);
        AddEdgesFromStateTable(edges, dto.ForkTable);
        AddEdgesFromJoinTable(edges, dto.JoinTable);
        return edges;
    }

    private static string GetNodeType(string state, CompiledDefinitionDto dto)
    {
        if (state == dto.InitialState)
            return "Start";
        if (dto.WaitTable is not null && dto.WaitTable.ContainsKey(state))
            return "Wait";
        if (dto.JoinTable is not null && dto.JoinTable.ContainsKey(state))
            return "Join";
        if (dto.ForkTable is not null && dto.ForkTable.ContainsKey(state))
            return "Fork";
        if (dto.Transitions is not null
            && dto.Transitions.TryGetValue(state, out var map)
            && map is not null
            && map.Values.Any(target => target?.End == true))
            return "End";
        return "Task";
    }

    /// <summary>
    /// 線形遷移テーブル（transitions）から到達先状態を収集する。
    /// </summary>
    private static void AddStatesFromTransitions(
        HashSet<string> stateNames,
        Dictionary<string, Dictionary<string, TransitionTargetDto>?>? transitions)
    {
        if (transitions is null) return;
        transitions.Keys.ToList().ForEach(key => _ = stateNames.Add(key));
        transitions.Values
            .Where(map => map is not null)
            .SelectMany(map => map!.Values)
            .ToList()
            .ForEach(target => AddTargetStates(stateNames, target));
    }

    /// <summary>
    /// 条件遷移テーブル（conditionalTransitions）から到達先状態を収集する。
    /// </summary>
    private static void AddStatesFromConditionalTransitions(
        HashSet<string> stateNames,
        Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? conditionalTransitions)
    {
        if (conditionalTransitions is null) return;
        conditionalTransitions.Keys.ToList().ForEach(key => _ = stateNames.Add(key));
        conditionalTransitions.Values
            .Where(map => map is not null)
            .SelectMany(map => map!.Values)
            .ToList()
            .ForEach(transition =>
            {
                AddTargetStates(stateNames, transition?.LinearTarget);
                AddTargetStates(stateNames, transition?.DefaultTarget);
                transition?.Cases?
                    .Select(c => c?.Target)
                    .ToList()
                    .ForEach(target => AddTargetStates(stateNames, target));
            });
    }

    /// <summary>
    /// stateName → stateName[] 形式テーブル（fork/join）から状態名を収集する。
    /// </summary>
    private static void AddStatesFromStateTable(
        HashSet<string> stateNames,
        Dictionary<string, List<string>?>? stateTable)
    {
        if (stateTable is null) return;
        stateTable.Keys.ToList().ForEach(key => _ = stateNames.Add(key));
        stateTable.Values
            .Where(list => list is not null)
            .SelectMany(list => list!)
            .ToList()
            .ForEach(state => _ = stateNames.Add(state));
    }

    /// <summary>
    /// wait テーブルのキー（待機状態名）を収集する。
    /// </summary>
    private static void AddStatesFromWaitTable(HashSet<string> stateNames, Dictionary<string, string>? waitTable)
    {
        if (waitTable is null) return;
        waitTable.Keys.ToList().ForEach(key => _ = stateNames.Add(key));
    }

    /// <summary>
    /// 単一遷移ターゲットから到達先状態を収集する。
    /// </summary>
    private static void AddTargetStates(HashSet<string> stateNames, TransitionTargetDto? target)
    {
        if (target is null)
        {
            return;
        }
        if (target.Next is { } n) stateNames.Add(n);
        target.Fork?.ToList().ForEach(state => _ = stateNames.Add(state));
    }

    /// <summary>
    /// 単一遷移ターゲットから描画用エッジを追加する。
    /// </summary>
    private static void AddEdges(List<GraphEdgeDefinition> edges, string from, TransitionTargetDto? target)
    {
        if (target is null)
        {
            return;
        }
        if (target.Next is { } to)
            edges.Add(new GraphEdgeDefinition { From = from, To = to });
        target.Fork?
            .Select(branch => new GraphEdgeDefinition { From = from, To = branch })
            .ToList()
            .ForEach(edges.Add);
    }

    /// <summary>
    /// 線形遷移テーブル（transitions）から描画用エッジを追加する。
    /// </summary>
    private static void AddEdgesFromTransitions(
        List<GraphEdgeDefinition> edges,
        Dictionary<string, Dictionary<string, TransitionTargetDto>?>? transitions)
    {
        if (transitions is null) return;
        transitions
            .Where(pair => pair.Value is not null)
            .SelectMany(pair => pair.Value!.Values.Select(target => (pair.Key, target)))
            .ToList()
            .ForEach(item => AddEdges(edges, item.Key, item.target));
    }

    /// <summary>
    /// 条件遷移テーブル（conditionalTransitions）から描画用エッジを追加する。
    /// </summary>
    private static void AddEdgesFromConditionalTransitions(
        List<GraphEdgeDefinition> edges,
        Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? conditionalTransitions)
    {
        if (conditionalTransitions is null) return;
        conditionalTransitions
            .Where(pair => pair.Value is not null)
            .SelectMany(pair => pair.Value!.Values.Select(transition => (pair.Key, transition)))
            .ToList()
            .ForEach(item =>
            {
                AddEdges(edges, item.Key, item.transition?.LinearTarget);
                AddEdges(edges, item.Key, item.transition?.DefaultTarget);
                item.transition?.Cases?
                    .Select(c => c?.Target)
                    .ToList()
                    .ForEach(target => AddEdges(edges, item.Key, target));
            });
    }

    /// <summary>
    /// stateName → stateName[] 形式テーブル（fork）から描画用エッジを追加する。
    /// </summary>
    private static void AddEdgesFromStateTable(List<GraphEdgeDefinition> edges, Dictionary<string, List<string>?>? stateTable)
    {
        if (stateTable is null) return;
        stateTable
            .Where(pair => pair.Value is not null)
            .SelectMany(pair => pair.Value!.Select(targetState => new GraphEdgeDefinition { From = pair.Key, To = targetState }))
            .ToList()
            .ForEach(edges.Add);
    }

    /// <summary>
    /// join テーブル（joinState ← dependencies）から描画用エッジを追加する。
    /// </summary>
    private static void AddEdgesFromJoinTable(List<GraphEdgeDefinition> edges, Dictionary<string, List<string>?>? joinTable)
    {
        if (joinTable is null) return;
        joinTable
            .Where(pair => pair.Value is not null)
            .SelectMany(pair => pair.Value!.Select(from => new GraphEdgeDefinition { From = from, To = pair.Key }))
            .ToList()
            .ForEach(edges.Add);
    }

    private sealed class CompiledDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string InitialState { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, TransitionTargetDto>?>? Transitions { get; set; } = [];
        public Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? ConditionalTransitions { get; set; } = [];
        public Dictionary<string, List<string>?>? ForkTable { get; set; } = [];
        public Dictionary<string, List<string>?>? JoinTable { get; set; } = [];
        public Dictionary<string, string>? WaitTable { get; set; } = [];
    }

    private sealed class CompiledFactTransitionDto
    {
        public TransitionTargetDto? LinearTarget { get; set; } = new();
        public List<CompiledTransitionCaseDto?>? Cases { get; set; } = [];
        public TransitionTargetDto? DefaultTarget { get; set; } = new();
    }

    private sealed class CompiledTransitionCaseDto
    {
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

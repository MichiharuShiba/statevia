using System.Text.Json;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Services;

/// <summary>定義の compiled_json から契約の Graph Definition（nodes / edges）を組み立てる。</summary>
internal sealed class GraphDefinitionService : IGraphDefinitionService
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICoreTransactionExecutor _executor;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionRepository _definitions;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IProjectAuthorizationService _projectAuth;

    public GraphDefinitionService(
        ICoreTransactionExecutor executor,
        IDisplayIdService displayIds,
        IDefinitionRepository definitions,
        ITenantContextAccessor tenantContext,
        IRuntimePermissionAuthorization runtimeAuth,
        IProjectAuthorizationService projectAuth)
    {
        _executor = executor;
        _displayIds = displayIds;
        _definitions = definitions;
        _tenantContext = tenantContext;
        _runtimeAuth = runtimeAuth;
        _projectAuth = projectAuth;
    }

    public async Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, CancellationToken ct = default)
    {
        await EnsureDefinitionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();

        var uuid = await _displayIds.ResolveAsync("definition", graphId, ct).ConfigureAwait(false);
        if (uuid == null)
            throw new NotFoundException("Graph not found");

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var detail = await _definitions
                    .GetLatestForApiAsync(uow, tenantId, uuid.Value, innerCt)
                    .ConfigureAwait(false);
                if (detail is null)
                    throw new NotFoundException("Graph not found");

                await _projectAuth
                    .EnsureCanReadAsync(uow, tenantId, detail.Definition.ProjectId, innerCt)
                    .ConfigureAwait(false);

                return BuildFromCompiledJson(graphId, detail.Definition.Name, detail.Version.CompiledJson);
            },
            ct).ConfigureAwait(false);
    }

    private Task EnsureDefinitionsReadAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, ct);

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
        AddStatesFromWaitEventRouteTable(stateNames, dto.WaitEventRouteTable);
        return stateNames;
    }

    /// <summary>
    /// 収集済み状態名から描画用ノード定義を構築する。
    /// </summary>
    private static List<GraphNodeDefinition> BuildNodes(IEnumerable<string> stateNames, CompiledDefinitionDto dto) =>
        stateNames.Select(state => new GraphNodeDefinition
        {
            NodeName = state,
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
        AddEdgesFromWaitEventRouteTable(edges, dto.WaitEventRouteTable);
        AddEdgesFromStateTable(edges, dto.ForkTable);
        // Join.all の依存がネスト枝先頭（Fork または内側へ進む action）のとき、
        // JoinTable から描くと幽霊枝になる。直接当該 Join へ着く依存だけ辺にする。
        AddEdgesFromJoinTable(edges, dto);
        return edges;
    }

    private static string GetNodeType(string state, CompiledDefinitionDto dto)
    {
        if (state == dto.InitialState)
            return "Start";
        if (dto.WaitEventRouteTable is not null && dto.WaitEventRouteTable.ContainsKey(state))
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
    /// WaitEventRouteTable のキー（待機状態名）と遷移先を収集する。
    /// </summary>
    private static void AddStatesFromWaitEventRouteTable(
        HashSet<string> stateNames,
        Dictionary<string, Dictionary<string, WaitEventRouteDto>?>? waitEventRouteTable)
    {
        if (waitEventRouteTable is null) return;
        foreach (var (stateName, routes) in waitEventRouteTable)
        {
            _ = stateNames.Add(stateName);
            if (routes is null) continue;
            routes.Values
                .Select(route => route?.Next)
                .Where(next => !string.IsNullOrWhiteSpace(next))
                .ToList()
                .ForEach(next => _ = stateNames.Add(next!));
        }
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
    /// <summary>
    /// WaitEventRouteTable からイベントごとのエッジ（Label = イベント名）を追加する。
    /// </summary>
    private static void AddEdgesFromWaitEventRouteTable(
        List<GraphEdgeDefinition> edges,
        Dictionary<string, Dictionary<string, WaitEventRouteDto>?>? waitEventRouteTable)
    {
        if (waitEventRouteTable is null) return;
        foreach (var (fromState, routes) in waitEventRouteTable)
        {
            if (routes is null) continue;
            foreach (var (eventName, route) in routes)
            {
                if (string.IsNullOrWhiteSpace(route?.Next)) continue;
                edges.Add(new GraphEdgeDefinition
                {
                    From = fromState,
                    To = route.Next,
                    Label = eventName
                });
            }
        }
    }

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
    /// <remarks>
    /// 依存が Fork 状態、または当該 Join へ直接着かない（枝内の内側 Fork へ進む action など）ときは辺を作らない。
    /// ネストの見た目の経路は内側 Join→外側 Join（transitions）であり、枝先頭→OuterJoin は幽霊枝になる。
    /// </remarks>
    private static void AddEdgesFromJoinTable(List<GraphEdgeDefinition> edges, CompiledDefinitionDto dto)
    {
        var joinTable = dto.JoinTable;
        if (joinTable is null)
            return;

        var forkTable = dto.ForkTable;
        joinTable
            .Where(pair => pair.Value is not null)
            .SelectMany(pair => pair.Value!
                .Where(from => ShouldDrawJoinTableEdge(from, pair.Key, dto, forkTable))
                .Select(from => new GraphEdgeDefinition { From = from, To = pair.Key }))
            .ToList()
            .ForEach(edges.Add);
    }

    /// <summary>
    /// Join.all 依存から Join への描画辺を付けるか。
    /// Fork 依存と、Join へ直接着かない枝先頭（ネスト途中の action）は付けない。
    /// </summary>
    private static bool ShouldDrawJoinTableEdge(
        string from,
        string joinState,
        CompiledDefinitionDto dto,
        Dictionary<string, List<string>?>? forkTable) =>
        !string.IsNullOrWhiteSpace(from)
        && (forkTable is null || !forkTable.ContainsKey(from))
        && DirectlyTargetsJoin(from, joinState, dto);

    /// <summary>遷移・Wait・条件辺のいずれかが <paramref name="joinState"/> を直接指すか。</summary>
    private static bool DirectlyTargetsJoin(string from, string joinState, CompiledDefinitionDto dto) =>
        TransitionMapTargetsJoin(dto.Transitions?.GetValueOrDefault(from), joinState)
        || WaitRoutesTargetJoin(dto.WaitEventRouteTable?.GetValueOrDefault(from), joinState)
        || ConditionalMapTargetsJoin(dto.ConditionalTransitions?.GetValueOrDefault(from), joinState);

    private static bool TransitionMapTargetsJoin(
        Dictionary<string, TransitionTargetDto>? map,
        string joinState) =>
        map is not null
        && map.Values.Any(target => TargetNextIsJoin(target, joinState));

    private static bool WaitRoutesTargetJoin(
        Dictionary<string, WaitEventRouteDto>? routes,
        string joinState) =>
        routes is not null
        && routes.Values.Any(route =>
            !string.IsNullOrWhiteSpace(route?.Next)
            && string.Equals(route.Next, joinState, StringComparison.OrdinalIgnoreCase));

    private static bool ConditionalMapTargetsJoin(
        Dictionary<string, CompiledFactTransitionDto>? map,
        string joinState) =>
        map is not null
        && map.Values.Any(transition =>
            TargetNextIsJoin(transition?.LinearTarget, joinState)
            || TargetNextIsJoin(transition?.DefaultTarget, joinState)
            || (transition?.Cases?.Any(c => TargetNextIsJoin(c?.Target, joinState)) ?? false));

    private static bool TargetNextIsJoin(TransitionTargetDto? target, string joinState) =>
        !string.IsNullOrWhiteSpace(target?.Next)
        && string.Equals(target.Next, joinState, StringComparison.OrdinalIgnoreCase);

    private sealed class CompiledDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string InitialState { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, TransitionTargetDto>?>? Transitions { get; set; } = [];
        public Dictionary<string, Dictionary<string, CompiledFactTransitionDto>?>? ConditionalTransitions { get; set; } = [];
        public Dictionary<string, List<string>?>? ForkTable { get; set; } = [];
        public Dictionary<string, List<string>?>? JoinTable { get; set; } = [];
        public Dictionary<string, Dictionary<string, WaitEventRouteDto>?>? WaitEventRouteTable { get; set; } = [];
    }

    private sealed class WaitEventRouteDto
    {
        public string Next { get; set; } = string.Empty;
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

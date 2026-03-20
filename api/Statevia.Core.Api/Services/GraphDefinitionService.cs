using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>定義の compiled_json から契約の Graph Definition（nodes / edges）を組み立てる。</summary>
public sealed class GraphDefinitionService : IGraphDefinitionService
{
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
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<CompiledDefinitionDto>(compiledJson, opts);
        if (dto is null)
            return new GraphDefinitionResponse { GraphId = graphId, Nodes = Array.Empty<GraphNodeDefinition>(), Edges = Array.Empty<GraphEdgeDefinition>() };

        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { dto.InitialState };
        if (dto.Transitions is not null)
        {
            foreach (var (from, map) in dto.Transitions)
            {
                stateNames.Add(from);
                if (map is null) continue;
                foreach (var (_, target) in map)
                {
                    if (target?.Next is { } n) stateNames.Add(n);
                    if (target?.Fork is { } f) foreach (var t in f) stateNames.Add(t);
                }
            }
        }
        if (dto.ForkTable is not null)
        {
            foreach (var key in dto.ForkTable.Keys)
                stateNames.Add(key);
            foreach (var list in dto.ForkTable.Values)
                if (list is not null)
                    foreach (var s in list) stateNames.Add(s);
        }
        if (dto.JoinTable is not null)
        {
            foreach (var key in dto.JoinTable.Keys)
                stateNames.Add(key);
            foreach (var list in dto.JoinTable.Values)
                if (list is not null)
                    foreach (var s in list) stateNames.Add(s);
        }
        if (dto.WaitTable is not null)
            foreach (var key in dto.WaitTable.Keys)
                stateNames.Add(key);

        var nodes = new List<GraphNodeDefinition>();
        foreach (var state in stateNames)
        {
            var nodeType = GetNodeType(state, dto);
            nodes.Add(new GraphNodeDefinition
            {
                NodeId = state,
                NodeType = nodeType,
                Label = state
            });
        }

        var edges = new List<GraphEdgeDefinition>();
        if (dto.Transitions is not null)
        {
            foreach (var (from, map) in dto.Transitions)
            {
                if (map is null) continue;
                foreach (var (_, target) in map)
                {
                    if (target is null) continue;
                    if (target.Next is { } to)
                        edges.Add(new GraphEdgeDefinition { From = from, To = to });
                    if (target.Fork is { } forkList)
                        foreach (var branch in forkList)
                            edges.Add(new GraphEdgeDefinition { From = from, To = branch });
                }
            }
        }
        if (dto.ForkTable is not null)
            foreach (var (from, list) in dto.ForkTable)
                if (list is not null)
                    foreach (var targetState in list)
                        edges.Add(new GraphEdgeDefinition { From = from, To = targetState });
        if (dto.JoinTable is not null)
            foreach (var (joinState, list) in dto.JoinTable)
                if (list is not null)
                    foreach (var from in list)
                        edges.Add(new GraphEdgeDefinition { From = from, To = joinState });

        return new GraphDefinitionResponse
        {
            GraphId = graphId,
            Nodes = nodes,
            Edges = edges,
            Ui = new GraphUiDefinition { Layout = "dagre" }
        };
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
        if (dto.Transitions is not null && dto.Transitions.TryGetValue(state, out var map) && map is not null)
            foreach (var (_, target) in map)
                if (target?.End == true)
                    return "End";
        return "Task";
    }

    private sealed class CompiledDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string InitialState { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, TransitionTargetDto>?>? Transitions { get; set; }
        public Dictionary<string, List<string>?>? ForkTable { get; set; }
        public Dictionary<string, List<string>?>? JoinTable { get; set; }
        public Dictionary<string, string>? WaitTable { get; set; }
    }

    private sealed class TransitionTargetDto
    {
        public string? Next { get; set; }
        public List<string>? Fork { get; set; }
        public bool End { get; set; }
    }
}

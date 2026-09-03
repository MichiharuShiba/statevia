using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json.Nodes;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>ComposeReadModelGraphJsonAsync の永続連携。</summary>
public sealed class ForkChildExecutionCoordinatorGraphComposeTests
{
    /// <summary>親 GET 合成で子の workerId が親 graph 上に現れる。</summary>
    [Fact]
    public async Task ComposeReadModelGraphJsonAsync_IncludesChildWorkerIdOnParentGraph()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string forkNodeId = "fork0001";
        const string parentGraph = """
            {
              "nodes": [
                {
                  "nodeId": "fork0001",
                  "nodeName": "ForkSrc",
                  "nodeType": "Task",
                  "startedAt": "2026-08-07T00:00:00Z",
                  "completedAt": "2026-08-07T00:00:01Z",
                  "fact": "Completed",
                  "attempt": 1,
                  "workerId": "parent-w"
                }
              ],
              "edges": []
            }
            """;
        const string childGraph = """
            {
              "nodes": [
                {
                  "nodeId": "child001",
                  "nodeName": "A",
                  "nodeType": "Wait",
                  "startedAt": "2026-08-07T00:00:01Z",
                  "attempt": 1,
                  "workerId": "worker-child-42"
                }
              ],
              "edges": []
            }
            """;
        await SeedAsync(db.Options, parentId, childId, forkNodeId, parentGraph, childGraph, now);
        var sut = CreateSut(db);

        // Act
        var composed = await sut.ComposeReadModelGraphJsonAsync(parentId, parentGraph, CancellationToken.None);

        // Assert
        var nodes = JsonNode.Parse(composed)!["nodes"]!.AsArray().OfType<JsonObject>().ToList();
        var childNode = Assert.Single(nodes, n => n["nodeId"]!.GetValue<string>() == "child001");
        Assert.Equal("worker-child-42", childNode["workerId"]!.GetValue<string>());

        var edges = JsonNode.Parse(composed)!["edges"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == forkNodeId
                && e["to"]!.GetValue<string>() == "child001"
                && e["type"]!.GetValue<int>() == 1);
    }

    /// <summary>直下の子 execution ID を列挙する。</summary>
    [Fact]
    public async Task ListDescendantExecutionIdsAsync_ReturnsDirectChild()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedAsync(db.Options, parentId, childId, "fork0001", """{"nodes":[],"edges":[]}""", """{"nodes":[],"edges":[]}""", now);
        var sut = CreateSut(db);

        // Act
        var ids = await sut.ListDescendantExecutionIdsAsync(parentId, CancellationToken.None);

        // Assert
        Assert.Equal([childId], ids);
    }

    private static ForkChildExecutionCoordinator CreateSut(InMemoryTestDatabase db)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executor = new TestCoreTransactionExecutor(uowFactory);
        return new ForkChildExecutionCoordinator(
            executor,
            new ExecutionRepository(),
            new ExecutionBranchRepository(),
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
            new NoOpWorkQueue(),
            new GuidIdGenerator(),
            new StubDisplayIdWrites(),
            new EventStoreRepository(new GuidIdGenerator()),
            Options.Create(new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 }),
            NullLogger<ForkChildExecutionCoordinator>.Instance);
    }

    private static async Task SeedAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childId,
        string forkNodeId,
        string parentGraph,
        string childGraph,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        ctx.Executions.AddRange(
            new ExecutionRow
            {
                ExecutionId = parentId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = definitionVersionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            },
            new ExecutionRow
            {
                ExecutionId = childId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = definitionVersionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
        ctx.ExecutionGraphSnapshots.AddRange(
            new ExecutionGraphSnapshotRow
            {
                ExecutionId = parentId,
                GraphJson = parentGraph
            },
            new ExecutionGraphSnapshotRow
            {
                ExecutionId = childId,
                GraphJson = childGraph
            });
        ctx.ExecutionBranches.Add(new ExecutionBranchRow
        {
            ParentExecutionId = parentId,
            ExecutionId = childId,
            ForkNodeId = forkNodeId,
            JoinState = "Join1",
            BranchState = "A",
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = now,
            UpdatedAt = now
        });
        await ctx.SaveChangesAsync();
    }

    private sealed class GuidIdGenerator : IIdGenerator
    {
        public Guid NewSequentialGuid() => Guid.NewGuid();
        public Guid NewRandomGuid() => Guid.NewGuid();
    }

    private sealed class StubDisplayIdWrites : IDisplayIdWriteService
    {
        public Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default) =>
            Task.FromResult(uuid.ToString("D"));
    }

    private sealed class NoOpWorkQueue : IExecutionWorkQueue
    {
        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct) => Task.CompletedTask;

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            Task.CompletedTask;

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct) =>
            Task.CompletedTask;

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            IReadOnlyList<string>? kinds,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}

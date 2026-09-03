using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using System.Data;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ForkExpansionHostHandler"/> の分岐配線。</summary>
public sealed class ForkExpansionHostHandlerTests
{
    /// <summary>不正な親 execution id は展開も Unload もしない。</summary>
    [Fact]
    public async Task HandleAsync_InvalidExecutionId_Skips()
    {
        // Arrange
        var coordinator = new RecordingCoordinator();
        var executions = new RecordingExecutionService();
        var sut = CreateSut(coordinator, executions, parent: null);

        // Act
        await sut.HandleAsync(CreateEvent("bad-id"), CancellationToken.None);

        // Assert
        Assert.False(coordinator.Called);
        Assert.False(executions.UnloadCalled);
    }

    /// <summary>親が無いとき展開も Unload もしない。</summary>
    [Fact]
    public async Task HandleAsync_ParentMissing_Skips()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var coordinator = new RecordingCoordinator();
        var executions = new RecordingExecutionService();
        var sut = CreateSut(coordinator, executions, parent: null);

        // Act
        await sut.HandleAsync(CreateEvent(parentId.ToString("D")), CancellationToken.None);

        // Assert
        Assert.False(coordinator.Called);
        Assert.False(executions.UnloadCalled);
    }

    /// <summary>展開成功時は親を Unload する。</summary>
    [Fact]
    public async Task HandleAsync_ExpandSucceeded_UnloadsParent()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var coordinator = new RecordingCoordinator
        {
            Result = new ForkExpansionResult(true, [Guid.NewGuid()], "Join1")
        };
        var executions = new RecordingExecutionService();
        var sut = CreateSut(coordinator, executions, CreateParent(parentId));

        // Act
        await sut.HandleAsync(CreateEvent(parentId.ToString("D")), CancellationToken.None);

        // Assert
        Assert.True(coordinator.Called);
        Assert.True(executions.UnloadCalled);
        Assert.Equal(parentId.ToString("D"), executions.LastEngineExecutionId);
        Assert.Equal("Start", executions.LastNodeId);
    }

    /// <summary>展開失敗時も親を Unload する。</summary>
    [Fact]
    public async Task HandleAsync_ExpandFailed_StillUnloadsParent()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var coordinator = new RecordingCoordinator
        {
            Result = new ForkExpansionResult(false, [], "Join1")
        };
        var executions = new RecordingExecutionService();
        var sut = CreateSut(coordinator, executions, CreateParent(parentId));

        // Act
        await sut.HandleAsync(CreateEvent(parentId.ToString("D")), CancellationToken.None);

        // Assert
        Assert.True(coordinator.Called);
        Assert.True(executions.UnloadCalled);
    }

    /// <summary>親に security snapshot が無いとき例外になる。</summary>
    [Fact]
    public async Task HandleAsync_MissingSecuritySnapshot_Throws()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var parent = CreateParent(parentId);
        parent.SecuritySnapshotJson = null;
        var sut = CreateSut(new RecordingCoordinator(), new RecordingExecutionService(), parent);

        // Act
        var act = () => sut.HandleAsync(CreateEvent(parentId.ToString("D")), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>定義版が無いとき例外になる。</summary>
    [Fact]
    public async Task HandleAsync_MissingDefinitionVersion_Throws()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var sut = CreateSut(
            new RecordingCoordinator(),
            new RecordingExecutionService(),
            CreateParent(parentId),
            definitionVersionMissing: true);

        // Act
        var act = () => sut.HandleAsync(CreateEvent(parentId.ToString("D")), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    private static ForkExpansionHostHandler CreateSut(
        IForkChildExecutionCoordinator coordinator,
        IExecutionService executionService,
        ExecutionRow? parent,
        bool definitionVersionMissing = false) =>
        new(
            new ReadOnlyExecutor(),
            new StubExecutionRepository(parent),
            new StubDefinitionRepository(definitionVersionMissing ? null : StubDefinitionRepository.CreateDefaultVersion()),
            new StubCompiler(),
            coordinator,
            executionService,
            NullLogger<ForkExpansionHostHandler>.Instance);

    private static ForkExpansionEvent CreateEvent(string executionId) =>
        new(
            ExecutionId: executionId,
            ForkState: "Start",
            SourceNodeId: "Start",
            Branches:
            [
                new ForkBranchExpansionPlan("A", null),
                new ForkBranchExpansionPlan("B", null)
            ]);

    private static ExecutionRow CreateParent(Guid parentId) =>
        new()
        {
            ExecutionId = parentId,
            TenantId = Guid.Parse("00000000-0000-4000-8000-000000000002"),
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false,
            SecuritySnapshotJson = """{"securityModelVersion":1}"""
        };

    private sealed class ReadOnlyExecutor : ICoreTransactionExecutor
    {
        public Task ExecuteReadCommittedAsync(
            Func<ICoreUnitOfWork, CancellationToken, Task> work,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<T> ExecuteReadCommittedAsync<T>(
            Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task ExecuteReadOnlyAsync(
            Func<ICoreUnitOfWork, CancellationToken, Task> work,
            CancellationToken cancellationToken = default) =>
            work(StubUnitOfWork.Instance, cancellationToken);

        public Task<T> ExecuteReadOnlyAsync<T>(
            Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) =>
            work(StubUnitOfWork.Instance, cancellationToken);
    }

    private sealed class StubUnitOfWork : ICoreUnitOfWork
    {
        public static readonly StubUnitOfWork Instance = new();
        public ICoreDatabase Db => throw new NotImplementedException();
        public Task BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubExecutionRepository(ExecutionRow? parent) : IExecutionRepository
    {
        public Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.FromResult(parent is not null && parent.ExecutionId == executionId ? parent : null);

        public Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            ExecutionListPageQuery query,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task AddExecutionAndSnapshotAsync(
            ICoreUnitOfWork uow,
            ExecutionRow execution,
            ExecutionGraphSnapshotRow snapshot,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task UpdateExecutionAndSnapshotAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string status,
            bool? cancelRequested,
            string graphJson,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class StubDefinitionRepository(DefinitionVersionRow? version) : IDefinitionRepository
    {
        public static DefinitionVersionRow CreateDefaultVersion() =>
            new()
            {
                DefinitionVersionId = Guid.NewGuid(),
                DefinitionId = Guid.NewGuid(),
                Version = 1,
                SourceYaml = "name: stub",
                CompiledJson = "{}",
                CreatedAt = DateTime.UtcNow
            };

        public Task<DefinitionDetail?> GetLatestForApiAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionRow?> GetLatestForMutationAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, int version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionVersionId, CancellationToken ct)
        {
            if (version is null)
                return Task.FromResult<DefinitionVersionRow?>(null);

            return Task.FromResult<DefinitionVersionRow?>(new DefinitionVersionRow
            {
                DefinitionVersionId = definitionVersionId,
                DefinitionId = version.DefinitionId,
                Version = version.Version,
                SourceYaml = version.SourceYaml,
                CompiledJson = version.CompiledJson,
                CreatedAt = version.CreatedAt
            });
        }

        public Task<DefinitionVersionRow?> GetVersionForApiAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, int version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<Guid?> ResolveProjectIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task AddWithInitialVersionAsync(
            ICoreUnitOfWork uow, DefinitionRow definition, DefinitionVersionRow version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionDetail?> PublishVersionAsync(
            ICoreUnitOfWork uow, DefinitionVersionPublishCommand command, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            ICoreUnitOfWork uow, Guid tenantId, DefinitionListPageQuery query, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, DateTime deletedAt, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionDetail?> RestoreAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> ExistsActiveSlugInProjectAsync(
            ICoreUnitOfWork uow, Guid projectId, string slug, Guid excludingDefinitionId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class StubCompiler : IDefinitionCompilerService
    {
        public CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson) =>
            new()
            {
                Name = "stub",
                InitialState = "Start",
                Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase),
                ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
                    StringComparer.OrdinalIgnoreCase),
                StateExecutorFactory = new DictionaryStateExecutorFactory(
                    new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase))
            };

        public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
            string name,
            string yaml,
            Guid? tenantId = null) =>
            throw new NotImplementedException();
    }

    private sealed class RecordingCoordinator : IForkChildExecutionCoordinator
    {
        public bool Called { get; private set; }
        public ForkExpansionResult Result { get; set; } = new(true, [], "Join1");

        public Task<ForkExpansionResult> ExpandForkAsync(ForkExpansionRequest request, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult(Result);
        }

        public Task<bool> NotifyChildTerminalAsync(
            Guid childExecutionId,
            string status,
            string? outputJson,
            string? statesJson,
            string? varsJson,
            CancellationToken ct) =>
            Task.FromResult(false);

        public Task<ForkJoinEvaluation> EvaluateJoinAsync(
            Guid parentExecutionId,
            string forkNodeId,
            CancellationToken ct) =>
            Task.FromResult(new ForkJoinEvaluation(ForkJoinEvaluationKind.Waiting, "Join1"));

        public Task<bool> HasPendingPhysicalJoinBranchesAsync(Guid parentExecutionId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task CascadeCancelToRunningChildrenAsync(Guid parentExecutionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<ForkWaitDeliveryTarget?> TryResolveChildWaitDeliveryAsync(
            Guid requestedExecutionId,
            string? nodeId,
            string eventName,
            CancellationToken ct) =>
            Task.FromResult<ForkWaitDeliveryTarget?>(null);

        public Task<string> ComposeReadModelGraphJsonAsync(
            Guid executionId,
            string baseGraphJson,
            CancellationToken ct) =>
            Task.FromResult(baseGraphJson);

        public Task<IReadOnlyList<Guid>> ListDescendantExecutionIdsAsync(
            Guid parentExecutionId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class RecordingExecutionService : IExecutionService
    {
        public bool UnloadCalled { get; private set; }
        public string? LastEngineExecutionId { get; private set; }
        public string? LastNodeId { get; private set; }

        public Task PersistCheckpointAndUnloadByEngineIdAsync(string engineExecutionId, string nodeId, CancellationToken ct)
        {
            UnloadCalled = true;
            LastEngineExecutionId = engineExecutionId;
            LastNodeId = nodeId;
            return Task.CompletedTask;
        }

        public Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ExecutionResponse> ExecuteQueuedStartAsync(Guid executionId, StartExecutionRequest request, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListPageQuery query, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) => throw new NotImplementedException();
        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task ResumeNodeAsync(
            string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey,
            CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task RecoverExecutionAsync(Guid executionId, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task CancelAsync(
            string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task PublishEventAsync(
            string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<long?> BeginOwnedSessionAsync(
            Guid executionId, string workerId, TimeSpan leaseDuration, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<bool> RenewOwnedSessionLeaseAsync(Guid executionId, TimeSpan leaseDuration, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) => throw new NotImplementedException();
        public Task AbandonLocalOwnedSessionAsync(Guid executionId) => throw new NotImplementedException();
        public Task AwaitLocalExecutionLoadAsync(Guid executionId, TimeSpan noProgressTimeout, CancellationToken ct) => throw new NotImplementedException();
    }
}

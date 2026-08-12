using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Statevia.Core.Application.Configuration;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionServiceTests
{
    private static readonly IOptions<EventDeliveryRetryOptions> DefaultEventDeliveryRetryOptions = Microsoft.Extensions.Options.Options.Create(
        new EventDeliveryRetryOptions
        {
            MaxAttempts = 3,
            BaseDelayMs = 0,
            MaxDelayMs = 1,
            Jitter = false,
            MaxTotalBackoffMs = 10_000
        });

    private static IHttpContextAccessor UnitTestHttpContextAccessor(string traceId = "trace-unit-test")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[RequestLogContext.TraceIdItemKey] = traceId;
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private sealed class FixedCorrelationIdAccessor(string correlationId = "trace-unit-test") : ICorrelationIdAccessor
    {
        public string GetCorrelationId() => correlationId;
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Guid _id;
        public FixedIdGenerator(Guid id) => _id = id;
        public Guid NewSequentialGuid() => _id;
        public Guid NewRandomGuid() => _id;
    }

    private sealed class DummyExecutorFactory : IStateExecutorFactory
    {
        public IStateExecutor? GetExecutor(string stateName) => null;
    }

    private sealed class FakeDisplayIdService : IDisplayIdService, IDisplayIdWriteService
    {
        public Guid? ResolveResultDefinition { get; set; }
        public Guid? ResolveResultExecution { get; set; }
        public string? AllocateResultWorkflow { get; set; } = "WF-DISP-1";
        public string? GetDisplayIdResult { get; set; }

        public Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default)
        {
            _ = uow;
            return Task.FromResult(AllocateResultWorkflow ?? uuid.ToString("D"));
        }

        public async Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return kind switch
            {
                "definition" => ResolveResultDefinition,
                "execution" => ResolveResultExecution,
                _ => null
            };
        }

        public async Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return GetDisplayIdResult;
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeExecutionEngine : IExecutionEngine
    {
        public ExecutionSnapshot? SnapshotToReturn { get; set; }
        public string GraphJsonToReturn { get; set; } = "{\"nodes\":[]}";
        public bool StartCalled { get; private set; }
        public object? LastInput { get; private set; }
        public string? LastEngineId { get; private set; }
        public CompiledWorkflowDefinition? LastDefinition { get; private set; }

        public bool CancelCalled { get; private set; }
        public string? PublishEventLastExecutionId { get; private set; }
        public string? PublishEventLastName { get; private set; }
        public Guid? PublishEventLastClientEventId { get; private set; }
        public Guid? CancelAsyncLastClientEventId { get; private set; }

        public string? ResumeWaitNodeLastExecutionId { get; private set; }
        public string? ResumeWaitNodeLastNodeId { get; private set; }
        public string? ResumeWaitNodeLastEventName { get; private set; }

        /// <summary>設定時、一致する <c>clientEventId</c> の Publish で <see cref="ApplyResult.AlreadyApplied"/> を返す。</summary>
        public Guid? PublishAlreadyAppliedWhenClientEventIdEquals { get; set; }

        /// <summary>設定時、一致する <c>clientEventId</c> の Cancel で <see cref="ApplyResult.AlreadyApplied"/> を返す。</summary>
        public Guid? CancelAlreadyAppliedWhenClientEventIdEquals { get; set; }

        public string Start(
            CompiledWorkflowDefinition definition,
            string? executionId = null,
            object? input = null,
            string? initialState = null)
        {
            StartCalled = true;
            LastDefinition = definition;
            LastInput = input;
            var resolvedId = executionId ?? "generated";
            LastEngineId = resolvedId;
            // 実 Engine 同様、Start 後は投影可能な Running スナップショットを返す。
            SnapshotToReturn ??= new ExecutionSnapshot
            {
                ExecutionId = resolvedId,
                WorkflowName = definition.Name,
                ActiveStates = string.IsNullOrWhiteSpace(initialState)
                    ? Array.Empty<string>()
                    : [initialState],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            };
            return resolvedId;
        }

        /// <summary>設定時、PublishEvent(executionId, name, clientEventId) がこの例外を投げる。</summary>
        public Exception? PublishEventExceptionToThrow { get; set; }

        /// <summary>設定時、ResumeWaitNode がこの例外を投げる。</summary>
        public Exception? ResumeWaitNodeExceptionToThrow { get; set; }

        public void ResumeWaitNode(string executionId, string nodeId, string eventName)
        {
            if (ResumeWaitNodeExceptionToThrow is { } resumeEx)
                throw resumeEx;

            ResumeWaitNodeLastExecutionId = executionId;
            ResumeWaitNodeLastNodeId = nodeId;
            ResumeWaitNodeLastEventName = eventName;
        }

        public void PublishEvent(string executionId, string eventName)
        {
            PublishEventLastExecutionId = executionId;
            PublishEventLastName = eventName;
            PublishEventLastClientEventId = null;
        }

        public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId)
        {
            if (PublishEventExceptionToThrow is { } publishEx)
                throw publishEx;

            PublishEventLastExecutionId = executionId;
            PublishEventLastName = eventName;
            PublishEventLastClientEventId = clientEventId;
            if (PublishAlreadyAppliedWhenClientEventIdEquals is { } publishDup && publishDup == clientEventId)
                return ApplyResult.AlreadyApplied;

            return ApplyResult.Applied;
        }

        public Task CancelAsync(string executionId)
        {
            CancelCalled = true;
            CancelAsyncLastClientEventId = null;
            return Task.CompletedTask;
        }

        public Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId)
        {
            CancelCalled = true;
            CancelAsyncLastClientEventId = clientEventId;
            if (CancelAlreadyAppliedWhenClientEventIdEquals is { } cancelDup && cancelDup == clientEventId)
                return Task.FromResult(ApplyResult.AlreadyApplied);

            return Task.FromResult(ApplyResult.Applied);
        }

        public ExecutionSnapshot? GetSnapshot(string executionId) => SnapshotToReturn;

        public string ExportExecutionGraph(string executionId) => GraphJsonToReturn;

        public void SetNodeCompletedHandler(Func<string, Task>? handler)
        {
            // no-op for tests
        }

        public void SetSuspendHandler(Func<string, string, Task>? handler)
        {
            // no-op for tests
        }

        public void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler)
        {
            // no-op for tests
        }

        public void CompletePhysicalJoin(
            string executionId,
            string joinStateName,
            IReadOnlyDictionary<string, object?> branchOutputs,
            IReadOnlyList<PhysicalJoinContextFragment>? contextMerges = null)
        {
            // no-op for tests
        }

        /// <summary>設定時、<see cref="ExportCheckpoint"/> がこの値を返す。</summary>
        public ExecutionRuntimeCheckpoint? CheckpointToExport { get; set; }

        /// <summary>設定時、<see cref="Unload"/> がこの例外を投げる。</summary>
        public Exception? UnloadExceptionToThrow { get; set; }

        /// <summary><see cref="ImportCheckpoint"/> が呼ばれた回数。</summary>
        public int ImportCheckpointCalls { get; private set; }

        public ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId) => CheckpointToExport;

        public void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint)
        {
            _ = definition;
            _ = checkpoint;
            ImportCheckpointCalls += 1;
            SnapshotToReturn ??= new ExecutionSnapshot
            {
                ExecutionId = checkpoint.ExecutionId,
                WorkflowName = checkpoint.DefinitionName,
                ActiveStates = checkpoint.ActiveStates,
                IsCompleted = checkpoint.IsCompleted,
                IsCancelled = checkpoint.IsCancelled,
                IsFailed = checkpoint.IsFailed
            };
        }

        /// <summary><see cref="Unload"/> が呼ばれた回数。</summary>
        public int UnloadCalls { get; private set; }

        public bool Unload(string executionId)
        {
            if (UnloadExceptionToThrow is { } unloadEx)
                throw unloadEx;

            UnloadCalls += 1;
            SnapshotToReturn = null;
            return true;
        }
    }

    private sealed class FakeProjectionUpdateQueue : IExecutionProjectionUpdateQueue
    {
        public int DrainCalls { get; private set; }
        public int EnqueueCalls { get; private set; }
        public Guid? LastDrainExecutionId { get; private set; }
        public Guid? LastEnqueueExecutionId { get; private set; }
        public Exception? DrainException { get; set; }

        public Task EnqueueAsync(Guid executionId, CancellationToken ct)
        {
            EnqueueCalls += 1;
            LastEnqueueExecutionId = executionId;
            return Task.CompletedTask;
        }

        public Task DrainAsync(Guid executionId, CancellationToken ct)
        {
            DrainCalls += 1;
            LastDrainExecutionId = executionId;
            if (DrainException is not null)
                return Task.FromException(DrainException);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventDeliveryDedupRepository : IEventDeliveryDedupRepository
    {
        private readonly ConcurrentDictionary<(Guid TenantId, Guid ExecutionId, Guid ClientEventId), EventDeliveryDedupRow> _rows = new();

        /// <summary>テスト用: 既存行を投入する。</summary>
        public void SeedRow(EventDeliveryDedupRow row) =>
            _rows[(row.TenantId, row.ExecutionId, row.ClientEventId)] = Clone(row);

        public Task<EventDeliveryDedupRow?> FindAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            CancellationToken cancellationToken)
        {
            _ = uow;
            return Task.FromResult(_rows.TryGetValue((tenantId, executionId, clientEventId), out var row) ? Clone(row) : null);
        }

        public Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken)
        {
            _ = uow;
            var key = (row.TenantId, row.ExecutionId, row.ClientEventId);
            if (!_rows.TryAdd(key, Clone(row)))
            {
                var inner = new PostgresException(
                    messageText: "duplicate key value violates unique constraint",
                    severity: "ERROR",
                    invariantSeverity: "ERROR",
                    sqlState: "23505");
                throw new DbUpdateException("duplicate key", inner);
            }

            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateStatusAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            EventDeliveryDedupStatusUpdate update,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(update);
            var key = (tenantId, executionId, clientEventId);
            if (!_rows.TryGetValue(key, out var row))
                return Task.FromResult(false);

            row.Status = update.Status;
            row.UpdatedAt = update.UtcNow;
            row.AppliedAt = update.AppliedAt;
            row.ErrorCode = update.ErrorCode;
            return Task.FromResult(true);
        }

        private static EventDeliveryDedupRow Clone(EventDeliveryDedupRow r) => new()
        {
            TenantId = r.TenantId,
            ExecutionId = r.ExecutionId,
            ClientEventId = r.ClientEventId,
            BatchId = r.BatchId,
            Status = r.Status,
            AcceptedAt = r.AcceptedAt,
            AppliedAt = r.AppliedAt,
            ErrorCode = r.ErrorCode,
            UpdatedAt = r.UpdatedAt
        };
    }

    /// <summary>先頭 N 回の <see cref="IEventDeliveryDedupRepository.InsertReceivedAsync"/> のみ例外を投げ、以降は通常の fake に委譲する。</summary>
    private sealed class FlakyThenSuccessEventDeliveryDedupRepository : IEventDeliveryDedupRepository
    {
        private readonly FakeEventDeliveryDedupRepository _inner = new();
        private readonly int _transientFailuresBeforeSuccess;
        private readonly Exception _transientFailure;

        public FlakyThenSuccessEventDeliveryDedupRepository(int transientFailuresBeforeSuccess, Exception transientFailure)
        {
            _transientFailuresBeforeSuccess = transientFailuresBeforeSuccess;
            _transientFailure = transientFailure;
        }

        /// <summary><see cref="InsertReceivedAsync"/> が呼ばれた累計回数。</summary>
        public int InsertReceivedCallCount { get; private set; }

        public Task<EventDeliveryDedupRow?> FindAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            CancellationToken cancellationToken) =>
            _inner.FindAsync(uow, tenantId, executionId, clientEventId, cancellationToken);

        public Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken)
        {
            InsertReceivedCallCount++;
            if (InsertReceivedCallCount <= _transientFailuresBeforeSuccess)
                throw _transientFailure;

            return _inner.AddReceivedAsync(uow, row, cancellationToken);
        }

        public Task<bool> TryUpdateStatusAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            EventDeliveryDedupStatusUpdate update,
            CancellationToken cancellationToken) =>
            _inner.TryUpdateStatusAsync(
                uow,
                tenantId,
                executionId,
                clientEventId,
                update,
                cancellationToken);
    }

    private sealed class FakeCommandDedupService : ICommandDedupService
    {
        private readonly CommandDedupKey? _keyToReturn;
        public string? LastRequestHash { get; private set; }
        public string? LastEndpoint { get; private set; }

        public FakeCommandDedupService(CommandDedupKey? keyToReturn) => _keyToReturn = keyToReturn;

        public CommandDedupKey? Create(string tenantKey, string? idempotencyKey, string method, string path, string? requestHash = null)
        {
            LastRequestHash = requestHash;
            LastEndpoint = $"{method} {path}".Trim();
            return _keyToReturn;
        }
    }

    private sealed class FakeCommandDedupRepository : ICommandDedupRepository
    {
        public CommandDedupRow? NextFindValid { get; set; }

        /// <summary>非 null のとき <see cref="FindValidConflictingRequestHashAsync"/> がこれを返す。</summary>
        public CommandDedupRow? NextConflictingRow { get; set; }

        public List<CommandDedupRow> SavedRows { get; } = [];

        public async Task<CommandDedupRow?> FindValidAsync(ICoreUnitOfWork uow, string dedupKey, DateTime utcNow, CancellationToken ct)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return NextFindValid;
        }

        public async Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
            ICoreUnitOfWork uow,
            string tenantKey,
            string endpoint,
            string idempotencyKey,
            string requestHash,
            DateTime utcNow,
            CancellationToken ct)
        {
            _ = uow;
            await Task.Yield();
            return NextConflictingRow;
        }

        public async Task SaveAsync(ICoreUnitOfWork uow, CommandDedupRow row, CancellationToken ct)
        {
            _ = uow;
            SavedRows.Add(row);
            await Task.Yield(); // async boundary for coverage
        }
    }

    private sealed class FakeExecutionRepository : IExecutionRepository
    {
        public ExecutionRow? ByIdResult { get; set; }
        public ExecutionGraphSnapshotRow? SnapshotByExecutionId { get; set; }

        public List<(ExecutionRow Execution, ExecutionGraphSnapshotRow Snapshot)> Added { get; } = [];
        public List<(Guid ExecutionId, string Status, bool? CancelRequested, string GraphJson)> Updates { get; } = [];
        public (int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items) ListWithDisplayIdsPageResult { get; set; } = (0, []);

        public async Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return ByIdResult;
        }

        public Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            GetByIdAsync(uow, Guid.Empty, executionId, ct);

        public async Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            ExecutionListPageQuery query,
            CancellationToken ct)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return ListWithDisplayIdsPageResult;
        }

        public async Task AddExecutionAndSnapshotAsync(
            ICoreUnitOfWork uow,
            ExecutionRow execution,
            ExecutionGraphSnapshotRow snapshot,
            CancellationToken ct)
        {
            _ = uow;
            Added.Add((execution, snapshot));
            await Task.Yield(); // async boundary for coverage
        }

        public async Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return SnapshotByExecutionId;
        }

        public async Task UpdateExecutionAndSnapshotAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string status,
            bool? cancelRequested,
            string graphJson,
            CancellationToken ct)
        {
            _ = uow;
            Updates.Add((executionId, status, cancelRequested, graphJson));
            await Task.Yield(); // async boundary for coverage
        }
    }

    private sealed class FakeExecutionCursorRepository : IExecutionCursorRepository
    {
        public Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct) => Task.CompletedTask;

        public Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) => Task.CompletedTask;

        public Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.FromResult<ExecutionCursorRow?>(null);
    }

    private sealed class FakeExecutionCheckpointStore : IExecutionCheckpointStore
    {
        public int UpsertCalls { get; private set; }
        public int AcquireCalls { get; private set; }
        public int RenewCalls { get; private set; }
        public int ClearOwnershipCalls { get; private set; }
        public int GenerationUpsertCalls { get; private set; }
        public long? AcquireGenerationToReturn { get; set; } = 1;
        public bool RenewResult { get; set; } = true;
        public bool GenerationUpsertResult { get; set; } = true;
        public ExecutionCheckpointDocument? DocumentById { get; set; }

        public Task UpsertAsync(
            ICoreUnitOfWork uow,
            ExecutionCheckpointDocument document,
            CancellationToken ct)
        {
            _ = uow;
            UpsertCalls += 1;
            DocumentById = document;
            return Task.CompletedTask;
        }

        public Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct)
        {
            _ = uow;
            _ = executionId;
            return Task.FromResult(DocumentById);
        }

        public Task DeleteAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) => Task.CompletedTask;

        public Task<long?> TryAcquireOwnershipAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string workerId,
            DateTime leaseUntilUtc,
            ExecutionCheckpointDocument? seed,
            CancellationToken ct)
        {
            _ = uow;
            _ = executionId;
            _ = workerId;
            _ = leaseUntilUtc;
            _ = seed;
            AcquireCalls += 1;
            return Task.FromResult(AcquireGenerationToReturn);
        }

        public Task<long?> TrySeizeExpiredOwnershipAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string workerId,
            DateTime nowUtc,
            DateTime newLeaseUntilUtc,
            CancellationToken ct) =>
            Task.FromResult<long?>(null);

        public Task<bool> TryRenewOwnershipLeaseAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            long ownerGeneration,
            DateTime leaseUntilUtc,
            CancellationToken ct)
        {
            _ = uow;
            _ = executionId;
            _ = ownerGeneration;
            _ = leaseUntilUtc;
            RenewCalls += 1;
            return Task.FromResult(RenewResult);
        }

        public Task<bool> TryUpsertRuntimeWithGenerationAsync(
            ICoreUnitOfWork uow,
            ExecutionCheckpointRuntimeUpsert upsert,
            CancellationToken ct)
        {
            _ = uow;
            _ = upsert;
            GenerationUpsertCalls += 1;
            return Task.FromResult(GenerationUpsertResult);
        }

        public Task<bool> TryClearOwnershipAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            long ownerGeneration,
            CancellationToken ct)
        {
            _ = uow;
            _ = executionId;
            _ = ownerGeneration;
            ClearOwnershipCalls += 1;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Guid>> ListExpiredOwnedExecutionIdsAsync(
            ICoreUnitOfWork uow,
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<bool> TryBumpGenerationAndClearExpiredOwnerAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            DateTime nowUtc,
            CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class FakeExecutionWaitRepository : IExecutionWaitRepository
    {
        /// <summary><see cref="DeleteByNodeIdAsync"/> に渡された (executionId, nodeId) の履歴。</summary>
        public List<(Guid ExecutionId, string NodeId)> DeletedByNodeId { get; } = [];

        /// <summary><see cref="ListByExecutionIdAsync"/> が返す wait 行。</summary>
        public IReadOnlyList<ExecutionWaitRow> ListByExecutionIdResult { get; set; } = Array.Empty<ExecutionWaitRow>();

        public Task ReplaceWaitsAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            IReadOnlyList<ExecutionWaitRow> waits,
            CancellationToken ct) => Task.CompletedTask;

        public Task DeleteByNodeIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string nodeId,
            CancellationToken ct)
        {
            DeletedByNodeId.Add((executionId, nodeId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) =>
            Task.FromResult(ListByExecutionIdResult);

        public Task<IReadOnlyList<ExecutionWaitRow>> ListExpiredDelayWaitsAsync(
            DateTime utcNow,
            int limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExecutionWaitRow>>(Array.Empty<ExecutionWaitRow>());

        public Task<IReadOnlyList<MatchingWaitSubscription>> ListMatchingSubscriptionsAsync(
            ICoreUnitOfWork uow,
            string topic,
            string correlationKey,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MatchingWaitSubscription>>(Array.Empty<MatchingWaitSubscription>());
    }

    private sealed class FakeEventStoreRepository : IEventStoreRepository
    {
        public List<(EventStoreEventType Type, Guid ExecutionId, string? Payload)> Appended { get; } = [];
        public List<EventStoreRow> AfterSeqItems { get; set; } = [];
        public bool AfterSeqHasMore { get; set; }
        public long MaxSeq { get; set; }

        private readonly HashSet<(Guid ExecutionId, Guid ClientEventId, EventStoreEventType Type)> _clientEventDedupKeys = [];

        /// <summary>設定時に追記処理で例外を投げて巻き戻し分岐を通す。</summary>
        public Exception? ThrowFromAppendWithDb { get; set; }

        public async Task AppendAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            EventStoreEventType eventType,
            string? payloadJson,
            CancellationToken ct = default)
        {
            _ = uow;
            if (ThrowFromAppendWithDb is { } ex)
                throw ex;

            Appended.Add((eventType, executionId, payloadJson));
            await Task.Yield(); // async boundary for coverage
        }

        public Task<bool> TryAppendIfAbsentByClientEventAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            Guid clientEventId,
            EventStoreEventType eventType,
            string? payloadJson,
            CancellationToken cancellationToken)
        {
            _ = uow;
            if (ThrowFromAppendWithDb is { } ex)
                throw ex;

            var key = (executionId, clientEventId, eventType);
            if (!_clientEventDedupKeys.Add(key))
                return Task.FromResult(false);

            Appended.Add((eventType, executionId, payloadJson));
            return Task.FromResult(true);
        }

        /// <summary>execution ごとの行。未設定時は <see cref="AfterSeqItems"/> を使う。</summary>
        public Dictionary<Guid, List<EventStoreRow>> ItemsByExecutionId { get; } = new();

        public async Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            long afterSeq,
            int limit,
            CancellationToken ct = default)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            var source = ItemsByExecutionId.TryGetValue(executionId, out var keyed)
                ? keyed
                : AfterSeqItems;
            var page = source
                .Where(r => r.Seq > afterSeq)
                .OrderBy(r => r.Seq)
                .Take(limit + 1)
                .ToList();
            var hasMore = page.Count > limit;
            if (!hasMore
                && ItemsByExecutionId.Count == 0
                && AfterSeqHasMore)
            {
                hasMore = true;
            }

            if (page.Count > limit)
                page.RemoveAt(page.Count - 1);
            return (page, hasMore);
        }

        public async Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct = default)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return MaxSeq;
        }
    }

    private static CompiledWorkflowDefinition DummyCompiledDefinition(string name)
    {
        return new CompiledWorkflowDefinition
        {
            Name = name,
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(),
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
            InitialState = "Initial",
            StateExecutorFactory = new DummyExecutorFactory()
        };
    }

    /// <summary>冪等キー一致かつキャッシュ本文が有効なとき再実行せず応答を返す。</summary>
    [Fact]
    public async Task StartAsync_WhenDedupHitAndCachedResponseValid_ReturnsCachedResponse()
    {
        // Arrange
        var defUuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var cached = new ExecutionResponse
        {
            DisplayId = "CACHED-DISP",
            ResourceId = defUuid, // will be overwritten by JSON if needed; kept for serialization consistency
            Status = "Running",
            StartedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var cachedJson = JsonSerializer.Serialize(cached, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = new CommandDedupRow
            {
                DedupKey = "d1",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "idem",
                ResponseBody = cachedJson,
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/executions", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService();
        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("x"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var executionRepo = new FakeExecutionRepository();
        var definitionsRepo = new StubDefinitionRepository();
        var dedupRepoRepo = dedupRepo;
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepoRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{\"a\":1}");
        var request = new StartExecutionRequest { DefinitionId = "def-1", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            request: request,
            idempotencyKey: "idem",
            requestContext: new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(dedupService.LastRequestHash));
        Assert.Empty(dedupRepo.SavedRows);
        Assert.Equal("CACHED-DISP", res.DisplayId);
        Assert.Equal(cached.ResourceId, res.ResourceId);
        Assert.Equal("Running", res.Status);

        Assert.False(engine.StartCalled);
    }

    /// <summary>冪等キーは同一だが要求本文が異なるとき 409 相当の例外を投げる。</summary>
    [Fact]
    public async Task StartAsync_WhenIdempotencyKeyReusedWithDifferentBody_ThrowsIdempotencyConflictException()
    {
        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = null,
            NextConflictingRow = new CommandDedupRow
            {
                DedupKey = "t1|POST /v1/executions:idem:deadbeef",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "idem",
                RequestHash = "deadbeef",
                StatusCode = StatusCodes.Status201Created,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d-new", Endpoint = "POST /v1/executions", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService();
        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("x"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var executionRepo = new FakeExecutionRepository();
        var definitionsRepo = new StubDefinitionRepository();
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{}");
        var request = new StartExecutionRequest { DefinitionId = "def-1", Input = inputDoc.RootElement };

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => sut.StartAsync(
            request: request,
            idempotencyKey: "idem",
            requestContext: new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None));

        Assert.False(engine.StartCalled);
    }

    /// <summary>冪等一致でもキャッシュ本文が壊れているとき通常起動して要求要約を保存する。</summary>
    [Fact]
    public async Task StartAsync_WhenDedupHitButCachedInvalid_ProceedsAndSavesDedupRequestHash()
    {
        // Arrange
        var defUuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var executionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var engineSnapshot = new ExecutionSnapshot
        {
            ExecutionId = executionId.ToString(),
            WorkflowName = "wf",
            ActiveStates = Array.Empty<string>(),
            IsCompleted = true,
            IsCancelled = false,
            IsFailed = false
        };

        // Act
        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = new CommandDedupRow
            {
                DedupKey = "d1",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "idem",
                ResponseBody = "{invalid-json",
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/executions", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            ResolveResultExecution = executionId,
            AllocateResultWorkflow = "WF-DISP-2",
            GetDisplayIdResult = null
        };

        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(executionId);
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = engineSnapshot,
            GraphJsonToReturn = "{\"nodes\":[{\"nodeId\":\"n1\",\"nodeName\":\"S\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}"
        };

        var executionRepo = new FakeExecutionRepository();

        var definitionsRepo = StubDefinitionRepositoryFactory.ForDefinition(defUuid, TestTenantIds.T1TenantId, "def");

        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{\"x\":true}");
        var request = new StartExecutionRequest { DefinitionId = "def-2", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            request: request,
            idempotencyKey: "idem",
            requestContext: new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        // Assert
        Assert.True(engine.StartCalled);
        Assert.Single(executionRepo.Added);
        Assert.Single(dedupRepo.SavedRows);
        Assert.Equal("WF-DISP-2", res.DisplayId);
        Assert.Equal(executionId, res.ResourceId);
        Assert.Equal("Completed", res.Status);
        Assert.False(string.IsNullOrWhiteSpace(dedupService.LastRequestHash));
        Assert.Equal(dedupService.LastRequestHash, dedupRepo.SavedRows[0].RequestHash);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowStarted, eventStore.Appended[0].Type);
    }

    /// <summary>冪等一致でも応答本文が空値のとき通常起動して重複抑止行を保存する。</summary>
    [Fact]
    public async Task StartAsync_WhenDedupHitButCachedResponseBodyNull_ProceedsAndSavesDedupRequestHash()
    {
        // Arrange
        var defUuid = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var executionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var engineSnapshot = new ExecutionSnapshot
        {
            ExecutionId = executionId.ToString(),
            WorkflowName = "wf",
            ActiveStates = Array.Empty<string>(),
            IsCompleted = true,
            IsCancelled = false,
            IsFailed = false
        };

        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = new CommandDedupRow
            {
                DedupKey = "d1",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "idem",
                ResponseBody = null,
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/executions", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            ResolveResultExecution = executionId,
            AllocateResultWorkflow = "WF-DISP-3",
            GetDisplayIdResult = null
        };

        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(executionId);
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = engineSnapshot,
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var executionRepo = new FakeExecutionRepository();
        var definitionsRepo = StubDefinitionRepositoryFactory.ForDefinition(defUuid, TestTenantIds.T1TenantId, "def");
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{\"x\":true}");
        var request = new StartExecutionRequest { DefinitionId = "def-2", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            request: request,
            idempotencyKey: "idem",
            requestContext: new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.True(engine.StartCalled);
        Assert.Single(executionRepo.Added);
        Assert.Single(dedupRepo.SavedRows);
        Assert.Equal("WF-DISP-3", res.DisplayId);
        Assert.Equal(executionId, res.ResourceId);
        Assert.Equal("Completed", res.Status);
        Assert.False(string.IsNullOrWhiteSpace(dedupService.LastRequestHash));
        Assert.Equal(dedupService.LastRequestHash, dedupRepo.SavedRows[0].RequestHash);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowStarted, eventStore.Appended[0].Type);
    }

    /// <summary>定義を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task StartAsync_WhenDefinitionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dedupService = new FakeCommandDedupService(null);
        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var display = new FakeDisplayIdService { ResolveResultDefinition = null };
        var engine = new FakeExecutionEngine();
        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var executionRepo = new FakeExecutionRepository();
        var definitionsRepo = new StubDefinitionRepository();
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());

        var request = new StartExecutionRequest { DefinitionId = "missing" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartAsync(request, idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions"), CancellationToken.None));
    }

    private static ExecutionService MakeSut(SqliteTestDatabase sqlite, out FakeExecutionRepository executionRepo)
    {
        executionRepo = new FakeExecutionRepository();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = Guid.NewGuid(), ResolveResultDefinition = Guid.NewGuid() };
        var compiler = new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var dedupService = new FakeCommandDedupService(null);
        var definitionsRepo = new StubDefinitionRepository();
        var dedupRepo = new FakeCommandDedupRepository();
        var eventStore = new FakeEventStoreRepository();

        return BuildExecutionService(
            sqlite,
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            executionRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository());
    }

    /// <summary>最大連番が零でも連番一の表示を返す。</summary>
    [Fact]
    public async Task GetExecutionViewAtSeqAsync_WhenMaxSeqIs0_ReturnsExecutionView()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"nodeName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"nodeName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository { MaxSeq = 0 },
            new FakeEventDeliveryDedupRepository());

        // Act
        var view = await sut.GetExecutionViewAtSeqAsync(idOrUuid: "display-or-uuid", atSeq: 1, CancellationToken.None);

        // Assert
        Assert.Equal("Running", view.Status);
        Assert.Equal(executionId.ToString("D"), view.ResourceId);
        Assert.Equal(2, view.Nodes.Count);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal("Task", view.Nodes[1].NodeType);
        Assert.Equal("S2", view.Nodes[1].NodeName);
        Assert.Equal("FAILED", view.Nodes[1].Status);
        Assert.Equal(defId.ToString("D"), view.GraphId);
    }

    /// <summary>実行識別子の解決に失敗したとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAtSeqAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();

        var display = new FakeDisplayIdService { ResolveResultExecution = null };
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetExecutionViewAtSeqAsync(idOrUuid: "x", atSeq: 1, CancellationToken.None));
    }

    /// <summary>指定連番が最大連番を超えるとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAtSeqAsync_WhenAtSeqOutOfRange_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"nodeName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"nodeName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository { MaxSeq = 5 },
            new FakeEventDeliveryDedupRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetExecutionViewAtSeqAsync(idOrUuid: "display-or-uuid", atSeq: 6, CancellationToken.None));
    }

    /// <summary>指定連番が範囲内で最大連番が正のとき表示を返す。</summary>
    [Fact]
    public async Task GetExecutionViewAtSeqAsync_WhenAtSeqWithinRangeAndMaxSeqNonZero_ReturnsExecutionView()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"nodeName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"nodeName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository { MaxSeq = 5 },
            new FakeEventDeliveryDedupRepository());

        // Act
        var view = await sut.GetExecutionViewAtSeqAsync(idOrUuid: "display-or-uuid", atSeq: 2, CancellationToken.None);

        // Assert
        Assert.Equal("Running", view.Status);
        Assert.Equal(executionId.ToString("D"), view.ResourceId);
        Assert.Equal(2, view.Nodes.Count);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal("Task", view.Nodes[1].NodeType);
        Assert.Equal("S2", view.Nodes[1].NodeName);
        Assert.Equal("FAILED", view.Nodes[1].Status);
        Assert.Equal(defId.ToString("D"), view.GraphId);
    }

    /// <summary>イベント記録を時系列へ変換し更新イベントに差分を付ける。</summary>
    [Fact]
    public async Task ListEventsAsync_MapsTimelineEventsAndPublishedGraphPatch()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var graphJson =
            "{\"nodes\":[" +
            "{\"nodeId\":\"a\",\"nodeName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
            "{\"nodeId\":\"b\",\"nodeName\":\"Wait\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
            "]}";

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            ResolveResultDefinition = executionRepo.ByIdResult!.DefinitionId,
            GetDisplayIdResult = null
        };

        var afterSeqItems = new List<EventStoreRow>
        {
            new EventStoreRow { ExecutionId = executionId, Seq = 1, Type = EventStoreEventType.WorkflowStarted.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { ExecutionId = executionId, Seq = 2, Type = EventStoreEventType.WorkflowCancelled.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { ExecutionId = executionId, Seq = 3, Type = EventStoreEventType.EventPublished.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { ExecutionId = executionId, Seq = 4, Type = "Unknown", OccurredAt = DateTime.UtcNow }
        };

        var eventStore = new FakeEventStoreRepository
        {
            AfterSeqItems = afterSeqItems,
            AfterSeqHasMore = false
        };

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            eventStore,
            new FakeEventDeliveryDedupRepository());

        // Act
        var res = await sut.ListEventsAsync(idOrUuid: "idOrUuid", afterSeq: 0, limit: 10, CancellationToken.None);

        // Assert
        Assert.False(res.HasMore);
        Assert.Equal(3, res.Events.Count); // Unknown is filtered out

        Assert.Equal("ExecutionStatusChanged", res.Events[0].Type);
        Assert.Equal("Running", res.Events[0].To);

        Assert.Equal("ExecutionStatusChanged", res.Events[1].Type);
        Assert.Equal("Cancelled", res.Events[1].To);

        Assert.Equal("GraphUpdated", res.Events[2].Type);
        Assert.NotNull(res.Events[2].Patch);
        Assert.NotNull(res.Events[2].Patch!.Nodes);
        Assert.Equal(2, res.Events[2].Patch!.Nodes!.Count);

        var nodeB = Assert.Single(res.Events[2].Patch!.Nodes!, n => n.NodeId == "b");
        Assert.True(nodeB.CanceledByExecution.GetValueOrDefault());
        Assert.Equal("CANCELED", nodeB.Status);
        Assert.Equal("Wait", nodeB.NodeName);
    }

    /// <summary>親 events GET で子孫の EventPublished を合成し、子 WorkflowStarted は落とす。</summary>
    [Fact]
    public async Task ListEventsAsync_ComposesDescendantEvents_AndDropsChildWorkflowStarted()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var t0 = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = parentId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = t0,
                UpdatedAt = t0,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = parentId,
                GraphJson = """{"nodes":[{"nodeId":"a","nodeName":"A"}],"edges":[]}""",
                UpdatedAt = t0
            }
        };
        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = parentId,
            GetDisplayIdResult = "eidCompose"
        };
        var eventStore = new FakeEventStoreRepository();
        eventStore.ItemsByExecutionId[parentId] =
        [
            new EventStoreRow
            {
                ExecutionId = parentId,
                Seq = 1,
                Type = EventStoreEventType.WorkflowStarted.ToPersistedString(),
                OccurredAt = t0
            }
        ];
        eventStore.ItemsByExecutionId[childId] =
        [
            new EventStoreRow
            {
                ExecutionId = childId,
                Seq = 1,
                Type = EventStoreEventType.WorkflowStarted.ToPersistedString(),
                OccurredAt = t0.AddSeconds(1)
            },
            new EventStoreRow
            {
                ExecutionId = childId,
                Seq = 2,
                Type = EventStoreEventType.EventPublished.ToPersistedString(),
                OccurredAt = t0.AddSeconds(2)
            }
        ];
        var coordinator = new FakeForkChildCoordinator { DescendantIds = [childId] };

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            eventStore,
            new FakeEventDeliveryDedupRepository(),
            forkChildCoordinator: coordinator);

        // Act
        var res = await sut.ListEventsAsync("eidCompose", afterSeq: 0, limit: 10, CancellationToken.None);

        // Assert
        Assert.False(res.HasMore);
        Assert.Equal(2, res.Events.Count);
        Assert.Equal("ExecutionStatusChanged", res.Events[0].Type);
        Assert.Equal("Running", res.Events[0].To);
        Assert.Equal("GraphUpdated", res.Events[1].Type);
        Assert.Equal("eidCompose", res.Events[1].ExecutionId);
        Assert.Equal(1, res.Events[0].Seq);
        Assert.Equal(2, res.Events[1].Seq);
    }

    /// <summary>取消要求で冪等一致かつ有効行があるとき副作用なく即時終了する。</summary>
    [Fact]
    public async Task CancelAsync_WhenDedupHitAndExistingNotNull_ReturnsEarly_WithoutSideEffects()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/cancel",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = new CommandDedupRow
            {
                DedupKey = dedupKey.DedupKey,
                Endpoint = dedupKey.Endpoint,
                IdempotencyKey = dedupKey.IdempotencyKey,
                RequestHash = null,
                StatusCode = 204,
                ResponseBody = null,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync(idOrUuid: "X", idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None);

        // Assert
        Assert.False(engine.CancelCalled);
        Assert.Empty(executionRepo.Updates);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>取消要求でドレインが失敗したとき、エンジン変異へ進まず例外を返す。</summary>
    [Fact]
    public async Task CancelAsync_WhenDrainFails_ThrowsAndSkipsEngineMutation()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString("D"),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var projectionQueue = new FakeProjectionUpdateQueue
        {
            DrainException = new InvalidOperationException("drain failed")
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository(),
            projectionQueue: projectionQueue);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CancelAsync(idOrUuid: "X", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None));
        Assert.Equal(1, projectionQueue.DrainCalls);
        Assert.Equal(executionId, projectionQueue.LastDrainExecutionId);
        Assert.False(engine.CancelCalled);
    }

    /// <summary>取消時に実行識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task CancelAsync_WhenExecutionResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = null };
        var executionRepo = new FakeExecutionRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CancelAsync(idOrUuid: "X", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None));

        Assert.False(engine.CancelCalled);
    }

    /// <summary>解決後に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task CancelAsync_WhenExecutionMissingAfterResolve_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CancelAsync(idOrUuid: "X", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None));

        Assert.False(engine.CancelCalled);
    }

    /// <summary>取消処理が通ると投影更新と追記保存まで実施する。</summary>
    [Fact]
    public async Task CancelAsync_WhenProceeding_UpdatesExecutionAndSnapshot_AppendsCancelledEvent_AndSavesDedupRow()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/cancel",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[{\"nodeId\":\"n1\",\"nodeName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}"
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId
        };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync(idOrUuid: "X", idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None);

        // Assert
        Assert.True(engine.CancelCalled);
        var expectedCancelClientEventId = ClientEventIdResolver.FromIdempotencyKey("idem", new FixedIdGenerator(Guid.NewGuid()));
        Assert.Equal(expectedCancelClientEventId, engine.CancelAsyncLastClientEventId);
        Assert.Single(executionRepo.Updates);
        Assert.Equal(executionId, executionRepo.Updates[0].ExecutionId);
        Assert.Equal("Cancelled", executionRepo.Updates[0].Status);
        Assert.True(executionRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, executionRepo.Updates[0].GraphJson);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowCancelled, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(TestTenantIds.T1TenantId.ToString("D"), payloadDoc.RootElement.GetProperty("tenantId").GetString());

        Assert.Single(dedupRepo.SavedRows);
        Assert.Null(dedupRepo.SavedRows[0].RequestHash);
        Assert.Equal(StatusCodes.Status204NoContent, dedupRepo.SavedRows[0].StatusCode);
        Assert.Null(dedupRepo.SavedRows[0].ResponseBody);
        Assert.Equal(dedupKey.DedupKey, dedupRepo.SavedRows[0].DedupKey);
        Assert.Equal(dedupKey.Endpoint, dedupRepo.SavedRows[0].Endpoint);
        Assert.Equal(dedupKey.IdempotencyKey, dedupRepo.SavedRows[0].IdempotencyKey);
    }

    /// <summary>イベント追記に失敗したとき巻き戻して例外を再送出する。</summary>
    [Fact]
    public async Task CancelAsync_WhenEventStoreAppendThrows_RollsBackAndRethrows()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository
        {
            ThrowFromAppendWithDb = new InvalidOperationException("append failed")
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CancelAsync(idOrUuid: "X", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None));

        // Assert
        Assert.Equal("append failed", ex.Message);
        Assert.True(engine.CancelCalled);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>
    /// 終端検知時は Unload 前に投影キュー排水と最終投影を行い、不完全 graph のまま Completed が残らないこと。
    /// </summary>
    [Fact]
    public async Task AwaitLocalExecutionLoadAsync_WhenTerminal_DrainsProjectsThenUnloads()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        const string finalGraphJson = """{"nodes":[{"id":"a"},{"id":"b"},{"id":"c"},{"id":"d"}]}""";
        using var sqlite = new SqliteTestDatabase();
        var projectionQueue = new FakeProjectionUpdateQueue();
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = true,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = finalGraphJson
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            projectionQueue);

        // Act
        await sut.AwaitLocalExecutionLoadAsync(executionId, CancellationToken.None);

        // Assert
        Assert.Equal(1, projectionQueue.DrainCalls);
        Assert.Equal(executionId, projectionQueue.LastDrainExecutionId);
        Assert.Single(executionRepo.Updates);
        Assert.Equal("Completed", executionRepo.Updates[0].Status);
        Assert.Equal(finalGraphJson, executionRepo.Updates[0].GraphJson);
        Assert.True(engine.UnloadCalls >= 1);
        Assert.Null(engine.SnapshotToReturn);
    }

    /// <summary>非公開投影更新処理が実行行とスナップショットを更新する。</summary>
    [Fact]
    public async Task UpdateProjectionFromEngineAsync_PrivateMethod_UpdatesExecutionAndSnapshot()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService();
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            eventStore,
            new FakeEventDeliveryDedupRepository());

        // Act
        await sut.UpdateProjectionFromEngineAsync(executionId, CancellationToken.None);

        // Assert
        Assert.Single(executionRepo.Updates);
        Assert.Equal("Cancelled", executionRepo.Updates[0].Status);
        Assert.True(executionRepo.Updates[0].CancelRequested.GetValueOrDefault());
        Assert.Equal(engine.GraphJsonToReturn, executionRepo.Updates[0].GraphJson);
    }

    /// <summary>イベント公開で冪等一致かつ有効行があるとき副作用なく即時終了する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenDedupHitAndExistingNotNull_ReturnsEarly_WithoutSideEffects()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository
        {
            NextFindValid = new CommandDedupRow
            {
                DedupKey = dedupKey.DedupKey,
                Endpoint = dedupKey.Endpoint,
                IdempotencyKey = dedupKey.IdempotencyKey,
                RequestHash = null,
                StatusCode = 204,
                ResponseBody = null,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        await sut.PublishEventAsync(idOrUuid: "X", eventName: "Approve", idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None);

        // Assert
        Assert.Null(engine.PublishEventLastName);
        Assert.Empty(executionRepo.Updates);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>イベント公開でドレインが失敗したとき、エンジン変異へ進まず例外を返す。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenDrainFails_ThrowsAndSkipsEngineMutation()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString("D"),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var projectionQueue = new FakeProjectionUpdateQueue
        {
            DrainException = new InvalidOperationException("drain failed")
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository(),
            projectionQueue: projectionQueue);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.PublishEventAsync(idOrUuid: "X", eventName: "Approve", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None));
        Assert.Equal(1, projectionQueue.DrainCalls);
        Assert.Equal(executionId, projectionQueue.LastDrainExecutionId);
        Assert.Null(engine.PublishEventLastExecutionId);
    }

    /// <summary>解決後に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenExecutionMissingAfterResolve_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.PublishEventAsync(idOrUuid: "X", eventName: "Approve", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None));

        Assert.Null(engine.PublishEventLastExecutionId);
        Assert.Null(engine.PublishEventLastName);
    }

    /// <summary>PublishEvent シム条件外の InvalidOperationException は 422（ApiValidationException）に写像する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenEngineRejectsShim_ThrowsApiValidationException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        const string engineMessage =
            "PublishEvent requires the active Wait state 'flow.approve.wait' to have exactly one event.";
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["flow.approve.wait"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn =
                """
                {"nodes":[
                  {"nodeId":"wait-1","nodeName":"flow.approve.wait","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","allowedEvents":["approve","reject"]}
                ]}
                """,
            PublishEventExceptionToThrow = new InvalidOperationException(engineMessage)
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: new FakeDisplayIdService { ResolveResultExecution = executionId },
            executionRepo: new FakeExecutionRepository
            {
                ByIdResult = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = TestTenantIds.T1TenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                }
            },
            eventStore: new FakeEventStoreRepository(),
            waitRepo: new FakeExecutionWaitRepository());

        // Act
        var ex = await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.PublishEventAsync(
                idOrUuid: "X",
                eventName: "approve",
                idempotencyKey: null,
                new CommandRequestContext("POST", "/v1/executions/events"),
                CancellationToken.None));

        // Assert
        Assert.Equal(engineMessage, ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    /// <summary>ResumeWaitNode の InvalidOperationException は 422（ApiValidationException）に写像する。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenEngineRejects_ThrowsApiValidationException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        const string engineMessage = "Event 'reject' is not allowed for Wait state 'flow.approve.wait'.";
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["flow.approve.wait"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn =
                """
                {"nodes":[
                  {"nodeId":"wait-1","nodeName":"flow.approve.wait","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","allowedEvents":["approve"]}
                ]}
                """,
            ResumeWaitNodeExceptionToThrow = new InvalidOperationException(engineMessage)
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: new FakeDisplayIdService { ResolveResultExecution = executionId },
            executionRepo: new FakeExecutionRepository
            {
                ByIdResult = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = TestTenantIds.T1TenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                }
            },
            eventStore: new FakeEventStoreRepository(),
            waitRepo: new FakeExecutionWaitRepository());

        // Act
        var ex = await Assert.ThrowsAsync<ApiValidationException>(() =>
            sut.ResumeNodeAsync(
                idOrUuid: "X",
                nodeId: "wait-1",
                resumeKey: "reject",
                idempotencyKey: null,
                new CommandRequestContext("POST", "/v1/executions/X/nodes/wait-1/resume"),
                CancellationToken.None));

        // Assert
        Assert.Equal(engineMessage, ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    /// <summary>PublishEvent 成功時、発行前の唯一アクティブ Wait nodeId で wait 行を先行削除する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenProceeding_ClearsSoleActiveWaitNodeId()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var waitRepo = new FakeExecutionWaitRepository();
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["Ask"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn =
                """
                {"nodes":[
                  {"nodeId":"wait-1","nodeName":"Ask","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":"Approve"}
                ]}
                """
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: new FakeDisplayIdService { ResolveResultExecution = executionId },
            executionRepo: new FakeExecutionRepository
            {
                ByIdResult = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = TestTenantIds.T1TenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                }
            },
            eventStore: new FakeEventStoreRepository(),
            waitRepo: waitRepo);

        // Act
        await sut.PublishEventAsync(
            idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None);

        // Assert
        Assert.Single(waitRepo.DeletedByNodeId);
        Assert.Equal(executionId, waitRepo.DeletedByNodeId[0].ExecutionId);
        Assert.Equal("wait-1", waitRepo.DeletedByNodeId[0].NodeId);
    }

    /// <summary>
    /// 発行前グラフから nodeId を解決できないとき、永続化済みの唯一 wait 行を削除対象にする。
    /// </summary>
    [Fact]
    public async Task PublishEventAsync_WhenGraphResolveFails_ClearsSolePersistedWaitNodeId()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var waitRepo = new FakeExecutionWaitRepository
        {
            ListByExecutionIdResult =
            [
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "persisted-wait",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["Approve"],
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["Ask"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            // nodes 欠落で TryResolveSoleActiveWaitNodeId が null になる
            GraphJsonToReturn = """{"notNodes":[]}"""
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: new FakeDisplayIdService { ResolveResultExecution = executionId },
            executionRepo: new FakeExecutionRepository
            {
                ByIdResult = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = TestTenantIds.T1TenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                }
            },
            eventStore: new FakeEventStoreRepository(),
            waitRepo: waitRepo);

        // Act
        await sut.PublishEventAsync(
            idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None);

        // Assert
        Assert.Single(waitRepo.DeletedByNodeId);
        Assert.Equal(executionId, waitRepo.DeletedByNodeId[0].ExecutionId);
        Assert.Equal("persisted-wait", waitRepo.DeletedByNodeId[0].NodeId);
    }

    /// <summary>
    /// 複数 wait 行から削除対象を決めるとき、eventName の前後空白を Trim して照合する。
    /// </summary>
    [Fact]
    public void TryResolveWaitNodeIdToClearFromPersisted_MatchesAllowedEvent_WhenEventNameHasWhitespace()
    {
        // Arrange
        var waits = new List<ExecutionWaitRow>
        {
            new()
            {
                ExecutionId = Guid.NewGuid(),
                NodeId = "wait-a",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["approve"],
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                ExecutionId = Guid.NewGuid(),
                NodeId = "wait-b",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["reject"],
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var nodeId = ExecutionService.TryResolveWaitNodeIdToClearFromPersisted(waits, "  approve  ");

        // Assert
        Assert.Equal("wait-a", nodeId);
    }

    /// <summary>イベント公開が通ると通知と投影更新と追記保存まで実施する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenProceeding_UpdatesExecutionAndSnapshot_AppendsEventPublished_AndSavesDedupRow()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = true
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId
        };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        const string eventName = "Approve";

        // Act
        await sut.PublishEventAsync(idOrUuid: "X", eventName: eventName, idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None);

        // Assert
        Assert.Equal(executionId.ToString(), engine.PublishEventLastExecutionId);
        Assert.Equal(eventName, engine.PublishEventLastName);
        var expectedClientEventId = ClientEventIdResolver.FromIdempotencyKey("idem", new FixedIdGenerator(Guid.NewGuid()));
        Assert.Equal(expectedClientEventId, engine.PublishEventLastClientEventId);

        Assert.Single(executionRepo.Updates);
        Assert.Equal(executionId, executionRepo.Updates[0].ExecutionId);
        Assert.Equal("Failed", executionRepo.Updates[0].Status);
        Assert.False(executionRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, executionRepo.Updates[0].GraphJson);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(TestTenantIds.T1TenantId.ToString("D"), payloadDoc.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(eventName, payloadDoc.RootElement.GetProperty("name").GetString());

        Assert.Single(dedupRepo.SavedRows);
        Assert.Null(dedupRepo.SavedRows[0].RequestHash);
        Assert.Equal(StatusCodes.Status204NoContent, dedupRepo.SavedRows[0].StatusCode);
        Assert.Null(dedupRepo.SavedRows[0].ResponseBody);
        Assert.Equal(dedupKey.DedupKey, dedupRepo.SavedRows[0].DedupKey);
        Assert.Equal(dedupKey.Endpoint, dedupRepo.SavedRows[0].Endpoint);
        Assert.Equal(dedupKey.IdempotencyKey, dedupRepo.SavedRows[0].IdempotencyKey);
    }

    /// <summary>Engine が AlreadyApplied のときも projection を更新し、冪等 event_store 追記が 1 回だけ行われる。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenEngineAlreadyApplied_UpdatesProjection_AndSingleDedupAppend()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "idem"
        };

        var expectedClientEventId = ClientEventIdResolver.FromIdempotencyKey("idem", new FixedIdGenerator(Guid.NewGuid()));

        var engine = new FakeExecutionEngine
        {
            PublishAlreadyAppliedWhenClientEventIdEquals = expectedClientEventId,
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();
        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        const string eventName = "Approve";

        // Act
        await sut.PublishEventAsync(idOrUuid: "X", eventName: eventName, idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None);

        // Assert
        Assert.Equal(expectedClientEventId, engine.PublishEventLastClientEventId);
        Assert.Single(executionRepo.Updates);
        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);
    }

    /// <summary>取消で Engine が AlreadyApplied のときも投影更新し、冪等 event_store 追記が 1 回だけ行われる。</summary>
    [Fact]
    public async Task CancelAsync_WhenEngineAlreadyApplied_UpdatesProjection_AndSingleDedupAppend()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions/cancel",
            IdempotencyKey = "idem"
        };

        var expectedClientEventId = ClientEventIdResolver.FromIdempotencyKey("idem", new FixedIdGenerator(Guid.NewGuid()));

        var engine = new FakeExecutionEngine
        {
            CancelAlreadyAppliedWhenClientEventIdEquals = expectedClientEventId,
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();
        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(dedupKey),
            dedupRepo: new FakeCommandDedupRepository { NextFindValid = null },
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync(idOrUuid: "X", idempotencyKey: "idem", new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None);

        // Assert
        Assert.Equal(expectedClientEventId, engine.CancelAsyncLastClientEventId);
        Assert.True(engine.CancelCalled);
        Assert.Single(executionRepo.Updates);
        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowCancelled, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);
    }

    /// <summary>イベント公開時の追記失敗で巻き戻して例外を再送出する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenEventStoreAppendThrows_RollsBackAndRethrows()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository
        {
            ThrowFromAppendWithDb = new InvalidOperationException("append failed")
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: dedupRepo,
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        const string eventName = "Approve";

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.PublishEventAsync(idOrUuid: "X", eventName: eventName, idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/events"), CancellationToken.None));

        // Assert
        Assert.Equal("append failed", ex.Message);
        Assert.Equal(executionId.ToString(), engine.PublishEventLastExecutionId);
        Assert.Equal(eventName, engine.PublishEventLastName);
        Assert.NotNull(engine.PublishEventLastClientEventId);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>event_delivery_dedup が既に APPLIED のとき Engine を呼ばずに終了する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenEventDeliveryAlreadyApplied_SkipsEngine()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var clientEventId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        var eventDedup = new FakeEventDeliveryDedupRepository();
        var now = DateTime.UtcNow;
        eventDedup.SeedRow(new EventDeliveryDedupRow
        {
            TenantId = TestTenantIds.T1TenantId,
            ExecutionId = executionId,
            ClientEventId = clientEventId,
            BatchId = null,
            Status = EventDeliveryDedupStatuses.Applied,
            AcceptedAt = now,
            AppliedAt = now,
            ErrorCode = null,
            UpdatedAt = now
        });

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            }
        };

        using var sqlite = new SqliteTestDatabase();
        var eventStore = new FakeEventStoreRepository();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            eventStore,
            eventDedup);

        // Act
        await sut.PublishEventAsync(idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: clientEventId.ToString(),
            requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None);

        // Assert
        Assert.Null(engine.PublishEventLastName);
        Assert.Empty(executionRepo.Updates);
        Assert.Empty(eventStore.Appended);
    }

    /// <summary>応答取得で実行識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionResponseAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = null };
        var executionRepo = new FakeExecutionRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetExecutionResponseAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>応答取得で実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionResponseAsync_WhenExecutionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultExecution = uuid };
        var executionRepo = new FakeExecutionRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetExecutionResponseAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>表示用識別子がないとき識別子文字列へフォールバックする。</summary>
    [Fact]
    public async Task GetExecutionResponseAsync_WhenDisplayIdMissing_FallsBackToUuidString()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var uuid = Guid.NewGuid();
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = uuid,
            GetDisplayIdResult = null
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act
        var res = await sut.GetExecutionResponseAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal(uuid.ToString("D"), res.DisplayId);
        Assert.Equal(uuid, res.ResourceId);
        Assert.Equal("Running", res.Status);
    }

    /// <summary>スナップショットがないときグラフ取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_WhenSnapshotMissing_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultExecution = uuid };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>グラフ取得時に識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = null };
        var executionRepo = new FakeExecutionRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>グラフ取得時に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_WhenExecutionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeExecutionEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultExecution = uuid };
        var executionRepo = new FakeExecutionRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>グラフ取得時、スナップショットの graphJson をそのまま返す。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_ReturnsGraphJsonAsIs()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultExecution = uuid };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = "{\"nodes\":[],\"edges\":[{\"from\":\"a1\",\"to\":\"b1\",\"type\":0}]}"
            }
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var graphJson = await sut.GetGraphJsonAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal("{\"nodes\":[],\"edges\":[{\"from\":\"a1\",\"to\":\"b1\",\"type\":0}]}", graphJson);
    }

    /// <summary>有効なノード識別子と再開キーで ResumeWaitNode と投影更新を実施する。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenValidNodeAndResumeKey_ResumesWaitNodeAndUpdatesExecution()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var nodeId = "node-1";
        var resumeKey = "Approve";

        using var sqlite = new SqliteTestDatabase();

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();

        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            eventStore,
            new FakeEventDeliveryDedupRepository());

        // Act
        await sut.ResumeNodeAsync(idOrUuid: "X", nodeId: nodeId, resumeKey: resumeKey, idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions"), CancellationToken.None);

        // Assert
        Assert.Equal(executionId.ToString(), engine.ResumeWaitNodeLastExecutionId);
        Assert.Equal(nodeId, engine.ResumeWaitNodeLastNodeId);
        Assert.Equal(resumeKey, engine.ResumeWaitNodeLastEventName);
        Assert.Null(engine.PublishEventLastExecutionId);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(TestTenantIds.T1TenantId.ToString("D"), payloadDoc.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(resumeKey, payloadDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal(nodeId, payloadDoc.RootElement.GetProperty("nodeId").GetString());

        Assert.Single(executionRepo.Updates);
        Assert.Equal("Cancelled", executionRepo.Updates[0].Status);
        Assert.True(executionRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, executionRepo.Updates[0].GraphJson);
    }

    /// <summary>一覧で表示用識別子が空値の行は識別子文字列を表示値に使う。</summary>
    [Fact]
    public async Task ListPagedAsync_WhenDisplayIdMissing_FallsBackToExecutionIdString()
    {
        // Arrange
        var w1 = new ExecutionRow
        {
            ExecutionId = Guid.NewGuid(),
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        };
        var w2 = new ExecutionRow
        {
            ExecutionId = Guid.NewGuid(),
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Completed",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = true,
            RestartLost = true
        };

        var executionRepo = new FakeExecutionRepository
        {
            ListWithDisplayIdsPageResult = (2, new List<(ExecutionRow Execution, string? DisplayId)>
            {
                (w1, null),
                (w2, "WF-DISP-2")
            })
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: new FakeDisplayIdService(),
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var page = await sut.ListPagedAsync(new ExecutionListPageQuery(new PageQuery(0, 10), new SortQuery(null, null), null, null, null), CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(w1.ExecutionId.ToString("D"), page.Items[0].DisplayId);
        Assert.Equal(w1.ExecutionId, page.Items[0].ResourceId);
        Assert.Equal("Running", page.Items[0].Status);
        Assert.Equal(w2.ExecutionId, page.Items[1].ResourceId);
        Assert.Equal("WF-DISP-2", page.Items[1].DisplayId);
        Assert.True(page.Items[1].CancelRequested);
        Assert.True(page.Items[1].RestartLost);
    }

    /// <summary>ページングで総件数と件数上限から続き有無を切り替える。</summary>
    [Fact]
    public async Task ListPagedAsync_HasMore_AndHasNotMore()
    {
        // Arrange
        var w1 = new ExecutionRow
        {
            ExecutionId = Guid.NewGuid(),
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var w2 = new ExecutionRow
        {
            ExecutionId = Guid.NewGuid(),
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var executionRepo = new FakeExecutionRepository
        {
            ListWithDisplayIdsPageResult = (3, new List<(ExecutionRow Execution, string? DisplayId)> { (w1, null), (w2, null) })
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: new FakeDisplayIdService(),
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var page = await sut.ListPagedAsync(new ExecutionListPageQuery(new PageQuery(0, 2), new SortQuery(null, null), null, null, null), CancellationToken.None);

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Items.Count);

        executionRepo.ListWithDisplayIdsPageResult = (2, new List<(ExecutionRow Execution, string? DisplayId)> { (w1, null), (w2, null) });

        // Act
        var page2 = await sut.ListPagedAsync(new ExecutionListPageQuery(new PageQuery(0, 2), new SortQuery(null, null), null, null, null), CancellationToken.None);

        // Assert
        Assert.Equal(2, page2.TotalCount);
        Assert.False(page2.HasMore);
    }

    /// <summary>定義 ID フィルタに一致しない場合は件数 0（リポジトリが空結果を返す）。</summary>
    [Fact]
    public async Task ListPagedAsync_DefinitionIdFilter_NoMatches_ReturnsEmptyPage()
    {
        // Arrange
        var unknownDefId = Guid.NewGuid();
        var executionRepo = new FakeExecutionRepository
        {
            ListWithDisplayIdsPageResult = (0, new List<(ExecutionRow Execution, string? DisplayId)>())
        };
        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: new FakeDisplayIdService(),
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var page = await sut.ListPagedAsync(new ExecutionListPageQuery(new PageQuery(0, 10), new SortQuery(null, null), null, unknownDefId, null), CancellationToken.None);

        // Assert
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }

    /// <summary>グラフ文字列からノード種別と状態と補完識別子を組み立てる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_Success_BuildsNodesAndGraphIdFallbacks()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var graphJson =
            "{\"nodes\":[" +
            "{\"nodeId\":\"n1\",\"nodeName\":null,\"nodeType\":\"Task\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null,\"input\":{\"seed\":1},\"output\":{\"next\":2},\"attempt\":2,\"workerId\":\"wk-1\",\"waitKey\":\"resume-1\",\"canceledByExecution\":false}," +
            "{\"nodeId\":null,\"nodeName\":\"S2\",\"nodeType\":\"Wait\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
            ",{\"nodeId\":\"n3\",\"nodeName\":\"S3\",\"nodeType\":\"Task\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"SomeOtherFact\"}," +
            "{\"nodeId\":\"n4\",\"nodeName\":\"S4\",\"nodeType\":\"End\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Completed\"}," +
            "{\"nodeId\":\"n5\",\"nodeName\":\"S5\",\"nodeType\":\"Join\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Joined\"}" +
            "]}";

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            GetDisplayIdResult = null
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal(executionId.ToString("D"), view.ResourceId);
        Assert.Equal(executionId.ToString("D"), view.DisplayId); // displayId fallback
        Assert.Equal(defId.ToString("D"), view.GraphId); // graphId fallback

        Assert.Equal(5, view.Nodes.Count);
        Assert.Equal("n1", view.Nodes[0].NodeId);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal(2, view.Nodes[0].Attempt);
        Assert.Equal("wk-1", view.Nodes[0].WorkerId);
        Assert.Equal("resume-1", view.Nodes[0].WaitKey);
        Assert.False(view.Nodes[0].CanceledByExecution);

        Assert.Equal(string.Empty, view.Nodes[1].NodeId);
        Assert.Equal("Wait", view.Nodes[1].NodeType);
        Assert.Equal("CANCELED", view.Nodes[1].Status);
        Assert.True(view.Nodes[1].CanceledByExecution);

        Assert.Equal("n3", view.Nodes[2].NodeId);
        Assert.Equal("Task", view.Nodes[2].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[2].Status); // default branch of MapNodeStatus
        Assert.False(view.Nodes[2].CanceledByExecution);

        Assert.Equal("n4", view.Nodes[3].NodeId);
        Assert.Equal("End", view.Nodes[3].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[3].Status); // Completed -> SUCCEEDED
        Assert.False(view.Nodes[3].CanceledByExecution);

        Assert.Equal("n5", view.Nodes[4].NodeId);
        Assert.Equal("Join", view.Nodes[4].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[4].Status); // Joined -> SUCCEEDED
        Assert.False(view.Nodes[4].CanceledByExecution);
    }

    /// <summary>グラフ文字列が空白のみのときノード一覧は空になる。</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task GetExecutionViewAsync_WhenGraphJsonIsWhitespace_ReturnsEmptyNodes(string graphJson)
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>グラフ文字列が不正なとき例外にせずノード一覧を空にする。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenGraphJsonInvalid_ReturnsEmptyNodes()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = "{not-json",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>ノード属性が空値のときノード一覧は空になる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenNodesPropertyIsNull_ReturnsEmptyNodes()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = "{\"nodes\":null}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>ノード属性が空配列のときノード一覧は空になる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenNodesArrayEmpty_ReturnsEmptyNodes()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = "{\"nodes\":[]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>スナップショット行がないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenSnapshotMissing_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByExecutionId = null
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>実行識別子を解決できないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>解決後に実行行がないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAsync_WhenExecutionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var executionRepo = new FakeExecutionRepository { ByIdResult = null };

        var display = new FakeDisplayIdService
        {
            ResolveResultExecution = executionId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetExecutionViewAsync(idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>RECEIVED 先行 INSERT が一時障害で失敗したあと再試行で成功する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenInsertReceivedTransientlyFails_RetriesThenSucceeds()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k-retry",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "550e8400-e29b-41d4-a716-446655440099"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };

        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();
        var flakyEventDelivery = new FlakyThenSuccessEventDeliveryDedupRepository(2, new IOException("transient db"));

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(dedupKey),
            executionRepo,
            new StubDefinitionRepository(),
            dedupRepo,
            eventStore,
            flakyEventDelivery);

        // Act
        await sut.PublishEventAsync(idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: dedupKey.IdempotencyKey,
            requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None);

        // Assert
        Assert.Equal(3, flakyEventDelivery.InsertReceivedCallCount);
        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
    }

    /// <summary>RECEIVED 先行 INSERT がキャンセルで失敗したとき再試行しない。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenInsertReceivedCanceled_DoesNotRetry()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k-cancel",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "550e8400-e29b-41d4-a716-446655440088"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var flakyEventDelivery = new FlakyThenSuccessEventDeliveryDedupRepository(5, new TaskCanceledException("canceled"));
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(dedupKey),
            executionRepo,
            new StubDefinitionRepository(),
            dedupRepo,
            new FakeEventStoreRepository(),
            flakyEventDelivery);

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.PublishEventAsync(idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: dedupKey.IdempotencyKey,
            requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None));

        Assert.Equal(1, flakyEventDelivery.InsertReceivedCallCount);
    }

    /// <summary>一時障害が続き最大試行回数に達したとき例外を伝播する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenInsertReceivedTransientExceedsMaxAttempts_Throws()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k-max",
            Endpoint = "POST /v1/executions/events",
            IdempotencyKey = "550e8400-e29b-41d4-a716-446655440077"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var flakyEventDelivery = new FlakyThenSuccessEventDeliveryDedupRepository(10, new IOException("always"));
        var strictRetryOptions = Microsoft.Extensions.Options.Options.Create(
            new EventDeliveryRetryOptions
            {
                MaxAttempts = 3,
                BaseDelayMs = 0,
                MaxDelayMs = 1,
                Jitter = false,
                MaxTotalBackoffMs = 10_000
            });

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(dedupKey),
            executionRepo,
            new StubDefinitionRepository(),
            dedupRepo,
            new FakeEventStoreRepository(),
            flakyEventDelivery,
            eventDeliveryRetryOptions: strictRetryOptions);

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => sut.PublishEventAsync(
            idOrUuid: "X",
            eventName: "Approve",
            idempotencyKey: dedupKey.IdempotencyKey,
            requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
            CancellationToken.None));

        Assert.Equal(3, flakyEventDelivery.InsertReceivedCallCount);
    }

    /// <summary>
    /// エンジンにインスタンスが無く投影が Running のとき、API 再起動喪失として引数例外（HTTP 422）を投げる。
    /// </summary>
    [Fact]
    public async Task PublishEventAsync_WhenEngineRuntimeMissing_AndExecutionRunning_ThrowsArgumentException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.PublishEventAsync(
                idOrUuid: "X",
                eventName: "Approve",
                idempotencyKey: null,
                requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
                CancellationToken.None));

        Assert.Contains("not loaded in this API process", ex.Message, StringComparison.Ordinal);
        Assert.Null(engine.PublishEventLastExecutionId);
    }

    /// <summary>
    /// エンジンにインスタンスが無く投影が Running のとき、キャンセルも同様に引数例外（HTTP 422）を投げる。
    /// </summary>
    [Fact]
    public async Task CancelAsync_WhenEngineRuntimeMissing_AndExecutionRunning_ThrowsArgumentException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CancelAsync(idOrUuid: "X", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions/cancel"), CancellationToken.None));

        Assert.Contains("not loaded in this API process", ex.Message, StringComparison.Ordinal);
        Assert.False(engine.CancelCalled);
    }

    /// <summary>
    /// エンジンにインスタンスが無く投影が終了済みのとき、終了後コマンド拒否として引数例外（HTTP 422）を投げる。
    /// </summary>
    [Fact]
    public async Task PublishEventAsync_WhenEngineRuntimeMissing_AndExecutionCompleted_ThrowsArgumentException()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defId,
                Status = "Completed",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.PublishEventAsync(
                idOrUuid: "X",
                eventName: "Approve",
                idempotencyKey: null,
                requestContext: new CommandRequestContext("POST", "/v1/executions/events"),
                CancellationToken.None));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(engine.PublishEventLastExecutionId);
    }

    /// <summary>定義 ID は解決できるが行が無いとき未検出例外を投げる。</summary>
    [Fact]
    public async Task StartAsync_WhenDefinitionRowMissingAfterResolve_ThrowsNotFound()
    {
        // Arrange
        var defUuid = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var display = new FakeDisplayIdService { ResolveResultDefinition = defUuid };
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            display,
            new FakeExecutionRepository(),
            new FakeEventStoreRepository(),
            projectionQueue: null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartAsync(
                new StartExecutionRequest { DefinitionId = "DEF-1" },
                idempotencyKey: null,
                new CommandRequestContext("POST", "/v1/executions"),
                CancellationToken.None));
    }

    /// <summary>永続化失敗時はトランザクションをロールバックして再送出する。</summary>
    [Fact]
    public async Task StartAsync_WhenEventStoreAppendFails_RethrowsAfterRollback()
    {
        // Arrange
        var defUuid = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var display = new FakeDisplayIdService { ResolveResultDefinition = defUuid };
        var eventStore = new FakeEventStoreRepository { ThrowFromAppendWithDb = new InvalidOperationException("db fail") };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(defUuid, TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            eventStore,
            new FakeEventDeliveryDedupRepository());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartAsync(
                new StartExecutionRequest { DefinitionId = "DEF-1" },
                idempotencyKey: null,
                new CommandRequestContext("POST", "/v1/executions"),
                CancellationToken.None));
    }

    /// <summary>存在確認でワークフローが無いとき未検出例外を投げる。</summary>
    [Fact]
    public async Task EnsureExecutionExistsAsync_Throws_WhenExecutionMissing()
    {
        // Arrange
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new FakeExecutionRepository { ByIdResult = null },
            new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureExecutionExistsAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>スナップショット JSON を execution ID で取得できる。</summary>
    [Fact]
    public async Task TryGetSnapshotGraphJsonByExecutionIdAsync_ReturnsGraphJson()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionRepo = new FakeExecutionRepository
        {
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = """{"nodes":[]}""",
                UpdatedAt = DateTime.UtcNow
            }
        };
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            executionRepo,
            new FakeEventStoreRepository());

        // Act
        var json = await sut.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, CancellationToken.None);

        // Assert
        Assert.Equal("""{"nodes":[]}""", json);
    }

    /// <summary>スナップショット行が無いとき null を返す。</summary>
    [Fact]
    public async Task TryGetSnapshotGraphJsonByExecutionIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new FakeExecutionRepository { SnapshotByExecutionId = null },
            new FakeEventStoreRepository());

        // Act
        var json = await sut.TryGetSnapshotGraphJsonByExecutionIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(json);
    }

    /// <summary>Engine にスナップショットが無いとき投影更新をスキップする。</summary>
    [Fact]
    public async Task UpdateProjectionFromEngineAsync_WhenEngineSnapshotNull_DoesNotUpdate()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionRepo = new FakeExecutionRepository();
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine { SnapshotToReturn = null },
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            new StubDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        // Act
        await sut.UpdateProjectionFromEngineAsync(executionId, CancellationToken.None);

        // Assert
        Assert.Empty(executionRepo.Updates);
    }

    private static ExecutionService MakeSut(
        FakeCommandDedupService dedupService,
        FakeCommandDedupRepository dedupRepo,
        FakeExecutionEngine engine,
        FakeDisplayIdService display,
        FakeExecutionRepository executionRepo,
        FakeEventStoreRepository eventStore,
        IExecutionProjectionUpdateQueue? projectionQueue = null,
        FakeExecutionWaitRepository? waitRepo = null)
    {
        var sqlite = new SqliteTestDatabase();
        return BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            dedupService,
            executionRepo,
            new StubDefinitionRepository(),
            dedupRepo,
            eventStore,
            new FakeEventDeliveryDedupRepository(),
            projectionQueue,
            waitRepo: waitRepo);
    }

    private sealed class FakeForkChildCoordinator : IForkChildExecutionCoordinator
    {
        public List<Guid> DescendantIds { get; init; } = [];
        public ForkJoinEvaluation EvaluateJoinResult { get; set; } =
            new(ForkJoinEvaluationKind.Waiting, "Join1");
        public int EvaluateJoinCalls { get; private set; }

        public Task<ForkExpansionResult> ExpandForkAsync(ForkExpansionRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<bool> NotifyChildTerminalAsync(
            Guid childExecutionId,
            string status,
            string? outputJson,
            string? statesJson,
            string? varsJson,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ForkJoinEvaluation> EvaluateJoinAsync(
            Guid parentExecutionId,
            string forkNodeId,
            CancellationToken ct)
        {
            EvaluateJoinCalls++;
            return Task.FromResult(EvaluateJoinResult);
        }

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
            Task.FromResult<IReadOnlyList<Guid>>(DescendantIds);
    }

    private static ExecutionService BuildExecutionService(
        SqliteTestDatabase sqlite,
        IExecutionEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        ICommandDedupService dedupService,
        IExecutionRepository executions,
        IDefinitionRepository definitions,
        ICommandDedupRepository dedup,
        IEventStoreRepository eventStore,
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IExecutionProjectionUpdateQueue? projectionUpdateQueue = null,
        Microsoft.Extensions.Options.IOptions<EventDeliveryRetryOptions>? eventDeliveryRetryOptions = null,
        IExecutionMutationPersistence? mutationPersistence = null,
        IProjectAuthorizationService? projectAuthorization = null,
        FakeExecutionWaitRepository? waitRepo = null,
        IForkChildExecutionCoordinator? forkChildCoordinator = null,
        IExecutionWorkQueue? workQueue = null,
        IExecutionCheckpointStore? checkpointStore = null,
        ExecutionOwnershipTracker? ownershipTracker = null)
    {
        if (displayIds is not IDisplayIdWriteService displayIdWrites)
            throw new InvalidOperationException("Test display id service must implement IDisplayIdWriteService.");

        var uowFactory = new TestCoreUnitOfWorkFactory(sqlite.Factory);
        var executor = new TestCoreTransactionExecutor(uowFactory);
        var retryOptions = eventDeliveryRetryOptions ?? DefaultEventDeliveryRetryOptions;
        mutationPersistence ??= new ExecutionMutationPersistence(
            uowFactory,
            eventDeliveryDedup,
            retryOptions,
            UnitTestHttpContextAccessor(),
            NullLogger<ExecutionMutationPersistence>.Instance);

        if (sqlite.TenantAccessor.TenantId == TestTenantIds.DefaultTenantId)
        {
            sqlite.TenantAccessor.Set(TestTenantIds.T1Context);
        }

        var runtimeAuth = new AllowAllRuntimePermissionAuthorization();
        var query = new ExecutionQueryService(
            displayIds,
            executions,
            runtimeAuth,
            sqlite.TenantAccessor,
            eventStore,
            executor,
            forkChildCoordinator);

        return new ExecutionService(
            engine,
            displayIds,
            compiler,
            idGenerator,
            dedupService,
            executions,
            new FakeExecutionCursorRepository(),
            waitRepo ?? new FakeExecutionWaitRepository(),
            definitions,
            projectAuthorization ?? new AllowAllProjectAuthorizationService(),
            runtimeAuth,
            new AllowAllExecutionMutationAuthorization(),
            new FakeExecutionSecuritySnapshotFactory(sqlite.TenantAccessor),
            sqlite.TenantAccessor,
            dedup,
            eventStore,
            eventDeliveryDedup,
            displayIdWrites,
            executor,
            mutationPersistence,
            NullLogger<ExecutionService>.Instance,
            retryOptions,
            new FixedCorrelationIdAccessor(),
            checkpointStore ?? new FakeExecutionCheckpointStore(),
            query,
            projectionUpdateQueue,
            workQueue,
            ownershipTracker,
            forkChildCoordinator: forkChildCoordinator);
    }

    /// <summary>Reader 付与テナントは Start（Executor）が 403。</summary>
    [Fact]
    public async Task StartAsync_ReaderGrant_ReturnsForbidden()
    {
        // Arrange
        var (sqlite, defId) = await SeedSharedDefinitionForStartAsync(ProjectAccessRole.Reader);
        var display = new FakeDisplayIdService { ResolveResultDefinition = defId };
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            projectAuthorization: new ProjectAuthorizationService(new ProjectRepository(new DefaultIdGenerator())));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartAsync(
                new StartExecutionRequest { DefinitionId = defId.ToString("D") },
                idempotencyKey: null,
                new CommandRequestContext("POST", "/v1/executions"),
                CancellationToken.None));

        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>Executor 付与テナントは Start できる。</summary>
    [Fact]
    public async Task StartAsync_ExecutorGrant_Succeeds()
    {
        // Arrange
        var (sqlite, defId) = await SeedSharedDefinitionForStartAsync(ProjectAccessRole.Executor);
        var display = new FakeDisplayIdService { ResolveResultDefinition = defId };
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            projectAuthorization: new ProjectAuthorizationService(new ProjectRepository(new DefaultIdGenerator())));

        // Act
        var response = await sut.StartAsync(
            new StartExecutionRequest { DefinitionId = defId.ToString("D") },
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, response.ResourceId);
    }

    /// <summary>ChildCompleted Resume で Join Waiting なら親投影を変更しない。</summary>
    [Fact]
    public async Task ResumeNodeAsync_ChildCompleted_WhenJoinWaiting_DoesNotUpdateParent()
    {
        // Arrange
        var executionId = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
        var coordinator = new FakeForkChildCoordinator
        {
            EvaluateJoinResult = new ForkJoinEvaluation(ForkJoinEvaluationKind.Waiting, "Join1")
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            new FakeDisplayIdService { ResolveResultExecution = executionId },
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            forkChildCoordinator: coordinator);

        // Act
        await sut.ResumeNodeAsync(
            executionId.ToString("D"),
            "fork-1",
            ExecutionWaitEventNames.ChildCompleted,
            idempotencyKey: null,
            new CommandRequestContext("WORKER", "/internal"),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, coordinator.EvaluateJoinCalls);
        Assert.Empty(executionRepo.Updates);
    }

    /// <summary>ChildCompleted Resume で Join Failed なら親を Failed に更新する。</summary>
    [Fact]
    public async Task ResumeNodeAsync_ChildCompleted_WhenJoinFailed_MarksParentFailed()
    {
        // Arrange
        var executionId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
        var coordinator = new FakeForkChildCoordinator
        {
            EvaluateJoinResult = new ForkJoinEvaluation(
                ForkJoinEvaluationKind.Failed,
                "Join1",
                FailureStatus: ExecutionProjectionStatuses.Failed)
        };
        var engine = new FakeExecutionEngine();
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            SnapshotByExecutionId = new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = """{"nodes":[],"edges":[]}""",
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService { ResolveResultExecution = executionId },
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            forkChildCoordinator: coordinator);

        // Act
        await sut.ResumeNodeAsync(
            executionId.ToString("D"),
            "fork-1",
            ExecutionWaitEventNames.ChildCompleted,
            idempotencyKey: null,
            new CommandRequestContext("WORKER", "/internal"),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, coordinator.EvaluateJoinCalls);
        Assert.Contains(executionRepo.Updates, u => u.Status == ExecutionProjectionStatuses.Failed);
        Assert.Equal(1, engine.UnloadCalls);
    }

    /// <summary>ChildCompleted Resume で Join Satisfied なら物理 Join 完了を呼ぶ。</summary>
    [Fact]
    public async Task ResumeNodeAsync_ChildCompleted_WhenJoinSatisfied_CompletesPhysicalJoin()
    {
        // Arrange
        var executionId = Guid.Parse("c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3");
        var coordinator = new FakeForkChildCoordinator
        {
            EvaluateJoinResult = new ForkJoinEvaluation(
                ForkJoinEvaluationKind.Satisfied,
                "Join1",
                CandidateInputs: new Dictionary<string, object?> { ["branchA"] = new { ok = true } })
        };
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "def",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = true,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = """{"nodes":[],"edges":[]}"""
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService { ResolveResultExecution = executionId },
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            forkChildCoordinator: coordinator);

        // Act
        await sut.ResumeNodeAsync(
            executionId.ToString("D"),
            "fork-1",
            ExecutionWaitEventNames.ChildCompleted,
            idempotencyKey: null,
            new CommandRequestContext("WORKER", "/internal"),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, coordinator.EvaluateJoinCalls);
        Assert.NotEmpty(executionRepo.Updates);
    }

    /// <summary>checkpoint がある Recover は Import して投影を更新する。</summary>
    [Fact]
    public async Task RecoverExecutionAsync_WhenCheckpointExists_ImportsAndUpdatesProjection()
    {
        // Arrange
        var defUuid = Guid.Parse("d4d4d4d4-d4d4-d4d4-d4d4-d4d4d4d4d4d4");
        var versionId = Guid.Parse("e5e5e5e5-e5e5-e5e5-e5e5-e5e5e5e5e5e5");
        var executionId = Guid.Parse("f6f6f6f6-f6f6-f6f6-f6f6-f6f6f6f6f6f6");
        var now = DateTime.UtcNow;
        var version = new DefinitionVersionRow
        {
            DefinitionVersionId = versionId,
            DefinitionId = defUuid,
            Version = 1,
            SourceYaml = "yaml",
            CompiledJson = "{}",
            CreatedAt = now
        };
        var definitionsRepo = new StubDefinitionRepository
        {
            LatestDetail = new DefinitionDetail
            {
                Definition = new DefinitionRow
                {
                    DefinitionId = defUuid,
                    TenantId = TestTenantIds.T1TenantId,
                    ProjectId = Guid.NewGuid(),
                    Slug = "def",
                    Name = "def",
                    LatestVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                Version = version
            },
            VersionById = version,
            VersionByNumber = version
        };
        var checkpoint = CreateMinimalCheckpoint(executionId.ToString());
        var checkpointJson = System.Text.Json.JsonSerializer.Serialize(
            checkpoint,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var checkpointStore = new FakeExecutionCheckpointStore
        {
            DocumentById = new ExecutionCheckpointDocument
            {
                ExecutionId = executionId,
                CheckpointJson = checkpointJson,
                SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                UpdatedAt = now
            }
        };
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = null,
            GraphJsonToReturn = """{"nodes":[],"edges":[]}"""
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defUuid,
                DefinitionVersionId = versionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = now,
                UpdatedAt = now
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            executionRepo,
            definitionsRepo,
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        await sut.RecoverExecutionAsync(
            executionId,
            new CommandRequestContext("WORKER", "/internal/execution-work-items"),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, engine.ImportCheckpointCalls);
        Assert.NotEmpty(executionRepo.Updates);
    }

    /// <summary>HTTP Cancel + 冪等キーで Cancel enqueue 時に dedup を保存する。</summary>
    [Fact]
    public async Task CancelAsync_WithWorkQueueAndIdempotency_SavesDedupAndEnqueues()
    {
        // Arrange
        var executionId = Guid.Parse("04040404-0404-0404-0404-040404040404");
        var workQueue = new CapturingWorkQueue();
        var dedupKey = new CommandDedupKey
        {
            DedupKey = "cancel-dedup",
            Endpoint = "POST /v1/executions/x/cancel",
            IdempotencyKey = "idem-cancel"
        };
        var dedupRepo = new FakeCommandDedupRepository();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(dedupKey),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            dedupRepo,
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            workQueue: workQueue);

        // Act
        await sut.CancelAsync(
            executionId.ToString("D"),
            idempotencyKey: "idem-cancel",
            new CommandRequestContext("POST", "/v1/executions/x/cancel"),
            CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Single(dedupRepo.SavedRows);
    }

    /// <summary>HTTP Start + 冪等キーで Start enqueue 時に dedup を保存する。</summary>
    [Fact]
    public async Task StartAsync_WithWorkQueueAndIdempotency_SavesDedupAndEnqueues()
    {
        // Arrange
        var defUuid = Guid.Parse("05050505-0505-0505-0505-050505050505");
        var executionId = Guid.Parse("06060606-0606-0606-0606-060606060606");
        var workQueue = new CapturingWorkQueue();
        var dedupKey = new CommandDedupKey
        {
            DedupKey = "start-dedup",
            Endpoint = "POST /v1/executions",
            IdempotencyKey = "idem-start"
        };
        var dedupRepo = new FakeCommandDedupRepository();
        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            AllocateResultWorkflow = "WF-Q-2",
            GetDisplayIdResult = "def-disp"
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(dedupKey),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(defUuid, TestTenantIds.T1TenantId, "def"),
            dedupRepo,
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            workQueue: workQueue);

        using var inputDoc = JsonDocument.Parse("{}");
        var request = new StartExecutionRequest { DefinitionId = "def-q", Input = inputDoc.RootElement };

        // Act
        var response = await sut.StartAsync(
            request,
            idempotencyKey: "idem-start",
            new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.Equal(executionId, response.ResourceId);
        Assert.Single(workQueue.Items);
        Assert.Single(dedupRepo.SavedRows);
    }

    /// <summary>Owned session 獲得失敗時は null を返す。</summary>
    [Fact]
    public async Task BeginOwnedSessionAsync_WhenAcquireFails_ReturnsNull()
    {
        // Arrange
        var executionId = Guid.Parse("01010101-0101-0101-0101-010101010101");
        var checkpointStore = new FakeExecutionCheckpointStore { AcquireGenerationToReturn = null };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        var generation = await sut.BeginOwnedSessionAsync(
            executionId,
            "worker-1",
            TimeSpan.FromMinutes(1),
            CancellationToken.None);

        // Assert
        Assert.Null(generation);
        Assert.Equal(1, checkpointStore.AcquireCalls);
    }

    /// <summary>ownership が無い Renew は true を返す。</summary>
    [Fact]
    public async Task RenewOwnedSessionLeaseAsync_WhenOwnershipMissing_ReturnsTrue()
    {
        // Arrange
        var executionId = Guid.Parse("02020202-0202-0202-0202-020202020202");
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        // Act
        var renewed = await sut.RenewOwnedSessionLeaseAsync(
            executionId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None);

        // Assert
        Assert.True(renewed);
    }

    /// <summary>KeepLoaded の fencing 失敗でも例外にしない。</summary>
    [Fact]
    public async Task PersistCheckpointKeepLoadedAsync_WhenGenerationUpsertFails_DoesNotThrow()
    {
        // Arrange
        var executionId = Guid.Parse("03030303-0303-0303-0303-030303030303");
        var ownership = new ExecutionOwnershipTracker();
        ownership.Set(executionId, "worker-1", 3);
        var checkpointStore = new FakeExecutionCheckpointStore { GenerationUpsertResult = false };
        var engine = new FakeExecutionEngine
        {
            CheckpointToExport = CreateMinimalCheckpoint(executionId.ToString())
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore,
            ownershipTracker: ownership);

        // Act
        await sut.PersistCheckpointKeepLoadedAsync(executionId.ToString(), CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpointStore.GenerationUpsertCalls);
    }

    /// <summary>HTTP Start で work queue があるとき Engine を回さず Start work item を enqueue する。</summary>
    [Fact]
    public async Task StartAsync_WithWorkQueue_EnqueuesStartWorkItemWithoutEngineStart()
    {
        // Arrange
        var defUuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var executionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var workQueue = new CapturingWorkQueue();
        var engine = new FakeExecutionEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            AllocateResultWorkflow = "WF-Q-1",
            GetDisplayIdResult = "def-disp"
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(defUuid, TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            workQueue: workQueue);

        using var inputDoc = JsonDocument.Parse("{}");
        var request = new StartExecutionRequest { DefinitionId = "def-q", Input = inputDoc.RootElement };

        // Act
        var response = await sut.StartAsync(
            request,
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.False(engine.StartCalled);
        Assert.Equal(executionId, response.ResourceId);
        Assert.Equal(ExecutionProjectionStatuses.Running, response.Status);
        Assert.Single(workQueue.Items);
        Assert.Equal(ExecutionWorkItemKinds.Start, workQueue.Items[0].Kind);
        Assert.Equal(executionId, workQueue.Items[0].ExecutionId);
    }

    /// <summary>HTTP Cancel で work queue があるとき Cancel work item を enqueue する。</summary>
    [Fact]
    public async Task CancelAsync_WithWorkQueue_EnqueuesCancelWorkItem()
    {
        // Arrange
        var executionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var workQueue = new CapturingWorkQueue();
        var display = new FakeDisplayIdService { ResolveResultExecution = executionId };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            display,
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            workQueue: workQueue);

        // Act
        await sut.CancelAsync(
            executionId.ToString("D"),
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions/x/cancel"),
            CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(ExecutionWorkItemKinds.Cancel, workQueue.Items[0].Kind);
        Assert.Equal(executionId, workQueue.Items[0].ExecutionId);
    }

    /// <summary>Owned session の獲得・延長・解放が checkpoint store を通る。</summary>
    [Fact]
    public async Task OwnedSession_BeginRenewEnd_UsesCheckpointStoreGenerations()
    {
        // Arrange
        var executionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var checkpointStore = new FakeExecutionCheckpointStore { AcquireGenerationToReturn = 7 };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        var generation = await sut.BeginOwnedSessionAsync(
            executionId,
            "worker-1",
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        var renewed = await sut.RenewOwnedSessionLeaseAsync(
            executionId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        await sut.EndOwnedSessionAsync(executionId, CancellationToken.None);

        // Assert
        Assert.Equal(7, generation);
        Assert.True(renewed);
        Assert.Equal(1, checkpointStore.AcquireCalls);
        Assert.Equal(1, checkpointStore.RenewCalls);
        Assert.Equal(1, checkpointStore.ClearOwnershipCalls);
    }

    /// <summary>ローカル所有放棄は Unload を呼び、Unload 失敗でも完了する。</summary>
    [Fact]
    public async Task AbandonLocalOwnedSessionAsync_UnloadFailure_StillCompletes()
    {
        // Arrange
        var executionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var ownership = new ExecutionOwnershipTracker();
        ownership.Set(executionId, "worker-1", 1);
        var engine = new FakeExecutionEngine
        {
            UnloadExceptionToThrow = new InvalidOperationException("unload failed")
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            ownershipTracker: ownership);

        // Act
        await sut.AbandonLocalOwnedSessionAsync(executionId);

        // Assert
        Assert.False(ownership.TryGet(executionId, out _, out _));
    }

    /// <summary>KeepLoaded checkpoint は ownership 無しで Upsert する。</summary>
    [Fact]
    public async Task PersistCheckpointKeepLoadedAsync_WithoutOwnership_UpsertsDocument()
    {
        // Arrange
        var executionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var checkpointStore = new FakeExecutionCheckpointStore();
        var engine = new FakeExecutionEngine
        {
            CheckpointToExport = CreateMinimalCheckpoint(executionId.ToString())
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        await sut.PersistCheckpointKeepLoadedAsync(executionId.ToString(), CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpointStore.UpsertCalls);
        Assert.Equal(0, checkpointStore.GenerationUpsertCalls);
    }

    /// <summary>KeepLoaded checkpoint は ownership ありで世代付き Upsert する。</summary>
    [Fact]
    public async Task PersistCheckpointKeepLoadedAsync_WithOwnership_UsesGenerationUpsert()
    {
        // Arrange
        var executionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var ownership = new ExecutionOwnershipTracker();
        ownership.Set(executionId, "worker-1", 3);
        var checkpointStore = new FakeExecutionCheckpointStore();
        var engine = new FakeExecutionEngine
        {
            CheckpointToExport = CreateMinimalCheckpoint(executionId.ToString())
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore,
            ownershipTracker: ownership);

        // Act
        await sut.PersistCheckpointKeepLoadedAsync(executionId.ToString(), CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpointStore.GenerationUpsertCalls);
        Assert.Equal(0, checkpointStore.UpsertCalls);
    }

    /// <summary>不正な executionId の KeepLoaded は何もしない。</summary>
    [Fact]
    public async Task PersistCheckpointKeepLoadedAsync_InvalidExecutionId_NoOps()
    {
        // Arrange
        var checkpointStore = new FakeExecutionCheckpointStore();
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine { CheckpointToExport = CreateMinimalCheckpoint("x") },
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        await sut.PersistCheckpointKeepLoadedAsync("not-a-guid", CancellationToken.None);

        // Assert
        Assert.Equal(0, checkpointStore.UpsertCalls);
    }

    /// <summary>キュー投入済み Start を Worker が実行する経路をカバーする。</summary>
    [Fact]
    public async Task ExecuteQueuedStartAsync_WhenExecutionExists_StartsEngineInProcess()
    {
        // Arrange
        var defUuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var versionId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var executionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var now = DateTime.UtcNow;
        var version = new DefinitionVersionRow
        {
            DefinitionVersionId = versionId,
            DefinitionId = defUuid,
            Version = 1,
            SourceYaml = "yaml",
            CompiledJson = "{}",
            CreatedAt = now
        };
        var definitionsRepo = new StubDefinitionRepository
        {
            LatestDetail = new DefinitionDetail
            {
                Definition = new DefinitionRow
                {
                    DefinitionId = defUuid,
                    TenantId = TestTenantIds.T1TenantId,
                    ProjectId = Guid.NewGuid(),
                    Slug = "def",
                    Name = "def",
                    LatestVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                Version = version
            },
            VersionById = version,
            VersionByNumber = version
        };
        var engine = new FakeExecutionEngine
        {
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "def",
                ActiveStates = ["Initial"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            }
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = defUuid,
                DefinitionVersionId = versionId,
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = now,
                UpdatedAt = now
            }
        };

        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService { AllocateResultWorkflow = "WF-QS", GetDisplayIdResult = "def-disp" },
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            executionRepo,
            definitionsRepo,
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{}");
        var request = new StartExecutionRequest { DefinitionId = "def-q", Input = inputDoc.RootElement };

        // Act
        var response = await sut.ExecuteQueuedStartAsync(executionId, request, CancellationToken.None);

        // Assert
        Assert.True(engine.StartCalled);
        Assert.Equal(executionId, response.ResourceId);
    }

    /// <summary>終端投影の Recover は Engine を触らず早期 return する。</summary>
    [Fact]
    public async Task RecoverExecutionAsync_WhenTerminal_ReturnsWithoutEngineLoad()
    {
        // Arrange
        var executionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var engine = new FakeExecutionEngine();
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Completed,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        // Act
        await sut.RecoverExecutionAsync(
            executionId,
            new CommandRequestContext("WORKER", "/internal/execution-work-items"),
            CancellationToken.None);

        // Assert
        Assert.Equal(0, engine.ImportCheckpointCalls);
        Assert.Equal(0, engine.UnloadCalls);
    }

    /// <summary>実行が無い ExecuteQueuedStart は NotFound を投げる。</summary>
    [Fact]
    public async Task ExecuteQueuedStartAsync_WhenExecutionMissing_ThrowsNotFound()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            new FakeExecutionEngine(),
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository { ByIdResult = null },
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        using var inputDoc = JsonDocument.Parse("{}");

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ExecuteQueuedStartAsync(
                Guid.NewGuid(),
                new StartExecutionRequest { DefinitionId = "x", Input = inputDoc.RootElement },
                CancellationToken.None));
    }

    /// <summary>checkpoint を保存して Unload する公開 API をカバーする。</summary>
    [Fact]
    public async Task PersistCheckpointAndUnloadAsync_WhenCheckpointExists_UnloadsEngine()
    {
        // Arrange
        var executionId = Guid.Parse("c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0");
        var checkpointStore = new FakeExecutionCheckpointStore();
        var engine = new FakeExecutionEngine
        {
            CheckpointToExport = CreateMinimalCheckpoint(executionId.ToString()),
            SnapshotToReturn = new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "def",
                ActiveStates = ["Wait"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = """{"nodes":[],"edges":[]}"""
        };
        var executionRepo = new FakeExecutionRepository
        {
            ByIdResult = new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            executionRepo,
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        await sut.PersistCheckpointAndUnloadAsync(executionId, "wait-1", CancellationToken.None);

        // Assert
        Assert.Equal(1, checkpointStore.UpsertCalls);
        Assert.Equal(1, engine.UnloadCalls);
        Assert.Single(executionRepo.Updates);
    }

    /// <summary>不正な Engine ID の Unload 経路は早期 return する。</summary>
    [Fact]
    public async Task PersistCheckpointAndUnloadByEngineIdAsync_InvalidId_NoOps()
    {
        // Arrange
        var checkpointStore = new FakeExecutionCheckpointStore();
        var engine = new FakeExecutionEngine
        {
            CheckpointToExport = CreateMinimalCheckpoint("x")
        };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository(),
            checkpointStore: checkpointStore);

        // Act
        await sut.PersistCheckpointAndUnloadByEngineIdAsync("not-a-guid", "n1", CancellationToken.None);

        // Assert
        Assert.Equal(0, checkpointStore.UpsertCalls);
        Assert.Equal(0, engine.UnloadCalls);
    }

    /// <summary>checkpoint が null のときは Unload しない。</summary>
    [Fact]
    public async Task PersistCheckpointAndUnloadAsync_WhenExportNull_SkipsUnload()
    {
        // Arrange
        var executionId = Guid.Parse("d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1");
        var engine = new FakeExecutionEngine { CheckpointToExport = null };
        using var sqlite = new SqliteTestDatabase();
        var sut = BuildExecutionService(
            sqlite,
            engine,
            new FakeDisplayIdService(),
            new StubDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(executionId),
            new FakeCommandDedupService(null),
            new FakeExecutionRepository(),
            StubDefinitionRepositoryFactory.ForDefinition(Guid.NewGuid(), TestTenantIds.T1TenantId, "def"),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository(),
            new FakeEventDeliveryDedupRepository());

        // Act
        await sut.PersistCheckpointAndUnloadAsync(executionId, "wait-1", CancellationToken.None);

        // Assert
        Assert.Equal(0, engine.UnloadCalls);
    }

    private static ExecutionRuntimeCheckpoint CreateMinimalCheckpoint(string executionId) =>
        new()
        {
            ExecutionId = executionId,
            DefinitionName = "def",
            ActiveStates = Array.Empty<string>(),
            StateAttempts = new Dictionary<string, int>(),
            StateOutputs = new Dictionary<string, JsonElement?>(),
            AppliedPublishClientEventIds = Array.Empty<Guid>(),
            AppliedCancelClientEventIds = Array.Empty<Guid>(),
            Context = new CheckpointContextData { States = new Dictionary<string, JsonElement?>() },
            Graph = new CheckpointGraphData { Nodes = [], Edges = [] },
            Join = new CheckpointJoinData
            {
                JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(),
                JoinSourceNodeIds = new Dictionary<string, IReadOnlyDictionary<string, string>>(),
                StartedJoins = Array.Empty<string>()
            },
            PendingWaits = Array.Empty<CheckpointPendingWait>()
        };

    private sealed class CapturingWorkQueue : IExecutionWorkQueue
    {
        public List<ExecutionWorkItemRow> Items { get; } = [];

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            EnqueueAsync(item, ct);

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
        {
            Items.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            EnqueueManyAsync(items, ct);

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            Task.FromResult(true);

        public Task ReleaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime availableAt,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            Task.FromResult(0);

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            Task.FromResult(0);
    }

    private static async Task<(SqliteTestDatabase Database, Guid DefinitionId)> SeedSharedDefinitionForStartAsync(
        ProjectAccessRole grantRole)
    {
        var sqlite = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var granteeTenantId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var granteeKey = grantRole == ProjectAccessRole.Reader ? "reader" : "executor";

        await using (var seed = sqlite.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            seed.Tenants.Add(new TenantRow
            {
                TenantId = granteeTenantId,
                TenantKey = granteeKey,
                DisplayName = "Grantee",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, ownerTenantId, defId, "shared-def", projectId, compiledJson: "{}");
            ProjectTestData.GrantAccess(seed, projectId, granteeTenantId, grantRole);
            await seed.SaveChangesAsync();
        }

        sqlite.TenantAccessor.Set(new TenantContextState(
            granteeTenantId,
            granteeKey,
            Guid.NewGuid(),
            TenantLifecycle.Active));

        return (sqlite, defId);
    }
}


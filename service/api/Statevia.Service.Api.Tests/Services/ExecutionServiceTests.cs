using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Persistence.Repositories;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Configuration;
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

    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Guid _id;
        public FixedIdGenerator(Guid id) => _id = id;
        public Guid NewGuid() => _id;
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

        /// <summary>設定時、一致する <c>clientEventId</c> の Publish で <see cref="ApplyResult.AlreadyApplied"/> を返す。</summary>
        public Guid? PublishAlreadyAppliedWhenClientEventIdEquals { get; set; }

        /// <summary>設定時、一致する <c>clientEventId</c> の Cancel で <see cref="ApplyResult.AlreadyApplied"/> を返す。</summary>
        public Guid? CancelAlreadyAppliedWhenClientEventIdEquals { get; set; }

        public string Start(CompiledWorkflowDefinition definition, string? executionId = null, object? input = null)
        {
            StartCalled = true;
            LastDefinition = definition;
            LastInput = input;
            LastEngineId = executionId;
            return executionId ?? "generated";
        }

        public void PublishEvent(string executionId, string eventName)
        {
            PublishEventLastExecutionId = executionId;
            PublishEventLastName = eventName;
            PublishEventLastClientEventId = null;
        }

        public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId)
        {
            PublishEventLastExecutionId = executionId;
            PublishEventLastName = eventName;
            PublishEventLastClientEventId = clientEventId;
            if (PublishAlreadyAppliedWhenClientEventIdEquals is { } publishDup && publishDup == clientEventId)
                return ApplyResult.AlreadyApplied;

            return ApplyResult.Applied;
        }

        public void PublishEvent(string eventName)
            => throw new NotSupportedException();

        public ApplyResult PublishEvent(string eventName, Guid clientEventId)
            => throw new NotSupportedException();

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

    private sealed class FakeExecutionWaitRepository : IExecutionWaitRepository
    {
        public Task ReplaceWaitsAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            IReadOnlyList<ExecutionWaitRow> waits,
            CancellationToken ct) => Task.CompletedTask;

        public Task DeleteByResumeTokenAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            string resumeToken,
            CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExecutionWaitRow>>(Array.Empty<ExecutionWaitRow>());
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

        public async Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            long afterSeq,
            int limit,
            CancellationToken ct = default)
        {
            _ = uow;
            await Task.Yield(); // async boundary for coverage
            return ((IReadOnlyList<EventStoreRow>)AfterSeqItems, AfterSeqHasMore);
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
            WaitTable = new Dictionary<string, string>(),
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
            GraphJsonToReturn = "{\"nodes\":[{\"nodeId\":\"n1\",\"stateName\":\"S\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}"
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

    /// <summary>連番指定が一未満のとき引数例外を投げる。</summary>
    [Fact]
    public async Task GetExecutionViewAtSeqAsync_WhenAtSeqLessThan1_ThrowsArgumentException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var sut = MakeSut(
            sqlite,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetExecutionViewAtSeqAsync(idOrUuid: "x", atSeq: 0, CancellationToken.None));
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
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
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
        Assert.Equal("S2", view.Nodes[1].StateName);
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
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
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
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
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
        Assert.Equal("S2", view.Nodes[1].StateName);
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
            "{\"nodeId\":\"a\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
            "{\"nodeId\":\"b\",\"stateName\":\"Wait\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
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

        var nodeB = Assert.Single(res.Events[2].Patch!.Nodes!, n => n.ExecutionNodeId == "b");
        Assert.True(nodeB.CanceledByExecution.GetValueOrDefault());
        Assert.Equal("CANCELED", nodeB.Status);
        Assert.Equal("Wait", nodeB.StateName);
    }

    /// <summary>ノード識別子が空白のみのとき引数例外を投げる。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenNodeIdWhitespace_ThrowsArgumentException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var sut = MakeSut(sqlite, out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ResumeNodeAsync(idOrUuid: "EXEC-1", nodeId: "  ", resumeKey: "Approve", idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions"), CancellationToken.None));
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
            GraphJsonToReturn = "{\"nodes\":[{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}"
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

    /// <summary>再開キーがないとき引数例外を投げる。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenResumeKeyMissing_ThrowsArgumentException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var sut = MakeSut(sqlite, out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ResumeNodeAsync(idOrUuid: "EXEC-1", nodeId: "node-1", resumeKey: null, idempotencyKey: null, new CommandRequestContext("POST", "/v1/executions"), CancellationToken.None));
    }

    /// <summary>有効なノード識別子と再開キーで公開と投影更新を実施する。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenValidNodeAndResumeKey_PublishesEventAndUpdatesExecution()
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
        Assert.Equal(executionId.ToString(), engine.PublishEventLastExecutionId);
        Assert.Equal(resumeKey, engine.PublishEventLastName);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(executionId, eventStore.Appended[0].ExecutionId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(TestTenantIds.T1TenantId.ToString("D"), payloadDoc.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(resumeKey, payloadDoc.RootElement.GetProperty("name").GetString());

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
        var page = await sut.ListPagedAsync(new ExecutionListQuery { Offset = 0, Limit = 10 }, CancellationToken.None);

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
        var page = await sut.ListPagedAsync(new ExecutionListQuery { Offset = 0, Limit = 2 }, CancellationToken.None);

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Items.Count);

        executionRepo.ListWithDisplayIdsPageResult = (2, new List<(ExecutionRow Execution, string? DisplayId)> { (w1, null), (w2, null) });

        // Act
        var page2 = await sut.ListPagedAsync(new ExecutionListQuery { Offset = 0, Limit = 2 }, CancellationToken.None);

        // Assert
        Assert.Equal(2, page2.TotalCount);
        Assert.False(page2.HasMore);
    }

    /// <summary>定義 id が解決できない場合は件数 0（一覧用フィルタ）。</summary>
    [Fact]
    public async Task ListPagedAsync_UnknownDefinitionId_ReturnsEmptyPage_WithoutQueryingPage()
    {
        // Arrange
        var display = new FakeDisplayIdService { ResolveResultDefinition = null };
        var executionRepo = new FakeExecutionRepository
        {
            ListWithDisplayIdsPageResult = (5, new List<(ExecutionRow Execution, string? DisplayId)> { })
        };
        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeExecutionEngine(),
            display: display,
            executionRepo: executionRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var page = await sut.ListPagedAsync(new ExecutionListQuery { Offset = 0, Limit = 10, DefinitionId = "no-such-def" }, CancellationToken.None);

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
            "{\"nodeId\":\"n1\",\"stateName\":null,\"nodeType\":\"Task\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null,\"input\":{\"seed\":1},\"output\":{\"next\":2},\"attempt\":2,\"workerId\":\"wk-1\",\"waitKey\":\"resume-1\",\"canceledByExecution\":false}," +
            "{\"nodeId\":null,\"stateName\":\"S2\",\"nodeType\":\"Wait\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
            ",{\"nodeId\":\"n3\",\"stateName\":\"S3\",\"nodeType\":\"Task\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"SomeOtherFact\"}," +
            "{\"nodeId\":\"n4\",\"stateName\":\"S4\",\"nodeType\":\"End\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Completed\"}," +
            "{\"nodeId\":\"n5\",\"stateName\":\"S5\",\"nodeType\":\"Join\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Joined\"}" +
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
        Assert.Equal("n1", view.Nodes[0].ExecutionNodeId);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal(2, view.Nodes[0].Attempt);
        Assert.Equal("wk-1", view.Nodes[0].WorkerId);
        Assert.Equal("resume-1", view.Nodes[0].WaitKey);
        Assert.False(view.Nodes[0].CanceledByExecution);

        Assert.Equal(string.Empty, view.Nodes[1].ExecutionNodeId);
        Assert.Equal("Wait", view.Nodes[1].NodeType);
        Assert.Equal("CANCELED", view.Nodes[1].Status);
        Assert.True(view.Nodes[1].CanceledByExecution);

        Assert.Equal("n3", view.Nodes[2].ExecutionNodeId);
        Assert.Equal("Task", view.Nodes[2].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[2].Status); // default branch of MapNodeStatus
        Assert.False(view.Nodes[2].CanceledByExecution);

        Assert.Equal("n4", view.Nodes[3].ExecutionNodeId);
        Assert.Equal("End", view.Nodes[3].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[3].Status); // Completed -> SUCCEEDED
        Assert.False(view.Nodes[3].CanceledByExecution);

        Assert.Equal("n5", view.Nodes[4].ExecutionNodeId);
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

    /// <summary>afterSeq が負のとき引数例外を投げる。</summary>
    [Fact]
    public async Task ListEventsAsync_WhenAfterSeqNegative_ThrowsArgumentException()
    {
        // Arrange
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            new FakeDisplayIdService { ResolveResultExecution = Guid.NewGuid() },
            new FakeExecutionRepository { ByIdResult = new ExecutionRow { ExecutionId = Guid.NewGuid(), TenantId = TestTenantIds.T1TenantId, DefinitionId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CancelRequested = false, RestartLost = false } },
            new FakeEventStoreRepository());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ListEventsAsync("WF-1", afterSeq: -1, limit: 10, CancellationToken.None));
        Assert.Contains("afterSeq", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>limit が範囲外のとき引数例外を投げる。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5001)]
    public async Task ListEventsAsync_WhenLimitOutOfRange_ThrowsArgumentException(int limit)
    {
        // Arrange
        var sut = MakeSut(
            new FakeCommandDedupService(null),
            new FakeCommandDedupRepository(),
            new FakeExecutionEngine(),
            new FakeDisplayIdService { ResolveResultExecution = Guid.NewGuid() },
            new FakeExecutionRepository { ByIdResult = new ExecutionRow { ExecutionId = Guid.NewGuid(), TenantId = TestTenantIds.T1TenantId, DefinitionId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CancelRequested = false, RestartLost = false } },
            new FakeEventStoreRepository());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ListEventsAsync("WF-1", afterSeq: 0, limit: limit, CancellationToken.None));
        Assert.Contains("limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ExecutionService MakeSut(
        FakeCommandDedupService dedupService,
        FakeCommandDedupRepository dedupRepo,
        FakeExecutionEngine engine,
        FakeDisplayIdService display,
        FakeExecutionRepository executionRepo,
        FakeEventStoreRepository eventStore,
        IExecutionProjectionUpdateQueue? projectionQueue = null)
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
            projectionQueue);
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
        IProjectAuthorizationService? projectAuthorization = null)
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

        return new ExecutionService(
            engine,
            displayIds,
            compiler,
            idGenerator,
            dedupService,
            executions,
            new FakeExecutionCursorRepository(),
            new FakeExecutionWaitRepository(),
            definitions,
            projectAuthorization ?? new AllowAllProjectAuthorizationService(),
            new AllowAllRuntimePermissionAuthorization(),
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
            UnitTestHttpContextAccessor(),
            projectionUpdateQueue);
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
            projectAuthorization: new ProjectAuthorizationService(new ProjectRepository()));

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
            projectAuthorization: new ProjectAuthorizationService(new ProjectRepository()));

        // Act
        var response = await sut.StartAsync(
            new StartExecutionRequest { DefinitionId = defId.ToString("D") },
            idempotencyKey: null,
            new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, response.ResourceId);
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


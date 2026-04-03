using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;
using Statevia.Core.Engine.Abstractions;
using System.Text.Json;

namespace Statevia.Core.Api.Tests.Services;

public sealed class WorkflowServiceTests
{
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

    private sealed class FakeDefinitionCompilerService : IDefinitionCompilerService
    {
        private readonly (CompiledWorkflowDefinition Compiled, string CompiledJson) _ret;
        public FakeDefinitionCompilerService((CompiledWorkflowDefinition Compiled, string CompiledJson) ret) => _ret = ret;
        public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml) => _ret;
    }

    private sealed class FakeDisplayIdService : IDisplayIdService
    {
        public Guid? ResolveResultDefinition { get; set; }
        public Guid? ResolveResultWorkflow { get; set; }
        public string? AllocateResultWorkflow { get; set; } = "WF-DISP-1";
        public string? GetDisplayIdResult { get; set; }
        public async Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return AllocateResultWorkflow ?? uuid.ToString("D");
        }

        public async Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return kind switch
            {
                "definition" => ResolveResultDefinition,
                "workflow" => ResolveResultWorkflow,
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

    private sealed class FakeWorkflowEngine : IWorkflowEngine
    {
        public WorkflowSnapshot? SnapshotToReturn { get; set; }
        public string GraphJsonToReturn { get; set; } = "{\"nodes\":[]}";
        public bool StartCalled { get; private set; }
        public object? LastInput { get; private set; }
        public string? LastEngineId { get; private set; }
        public CompiledWorkflowDefinition? LastDefinition { get; private set; }

        public bool CancelCalled { get; private set; }
        public string? PublishEventLastWorkflowId { get; private set; }
        public string? PublishEventLastName { get; private set; }

        public string Start(CompiledWorkflowDefinition definition, string? workflowId = null, object? workflowInput = null)
        {
            StartCalled = true;
            LastDefinition = definition;
            LastInput = workflowInput;
            LastEngineId = workflowId;
            return workflowId ?? "generated";
        }

        public void PublishEvent(string workflowId, string eventName)
        {
            PublishEventLastWorkflowId = workflowId;
            PublishEventLastName = eventName;
        }

        public void PublishEvent(string eventName)
            => throw new NotSupportedException();

        public Task CancelAsync(string workflowId)
        {
            CancelCalled = true;
            return Task.CompletedTask;
        }

        public WorkflowSnapshot? GetSnapshot(string workflowId) => SnapshotToReturn;

        public string ExportExecutionGraph(string workflowId) => GraphJsonToReturn;
    }

    private sealed class FakeCommandDedupService : ICommandDedupService
    {
        private readonly CommandDedupKey? _keyToReturn;
        public string? LastRequestHash { get; private set; }
        public string? LastEndpoint { get; private set; }

        public FakeCommandDedupService(CommandDedupKey? keyToReturn) => _keyToReturn = keyToReturn;

        public CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path, string? requestHash = null)
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

        public List<CommandDedupRow> SavedRows { get; } = new();

        public async Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            return NextFindValid;
        }

        public async Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
            string tenantId,
            string endpoint,
            string idempotencyKey,
            string requestHash,
            DateTime utcNow,
            CancellationToken ct)
        {
            await Task.Yield();
            return NextConflictingRow;
        }

        public async Task SaveAsync(CommandDedupRow row, CancellationToken ct)
        {
            SavedRows.Add(row);
            await Task.Yield(); // async boundary for coverage
        }

        public async Task SaveAsync(CoreDbContext db, CommandDedupRow row, CancellationToken ct)
        {
            SavedRows.Add(row);
            await Task.Yield(); // async boundary for coverage
        }
    }

    private sealed class FakeWorkflowRepository : IWorkflowRepository
    {
        public WorkflowRow? ByIdResult { get; set; }
        public ExecutionGraphSnapshotRow? SnapshotByWorkflowId { get; set; }

        public List<(WorkflowRow Workflow, ExecutionGraphSnapshotRow Snapshot)> Added { get; } = new();
        public List<(Guid WorkflowId, string Status, bool? CancelRequested, string GraphJson)> Updates { get; } = new();
        public List<(WorkflowRow Workflow, string? DisplayId)> ListWithDisplayIdsResult { get; set; } = new();
        public (int TotalCount, List<(WorkflowRow Workflow, string? DisplayId)> Items) ListWithDisplayIdsPageResult { get; set; } = (0, new());

        public async Task<WorkflowRow?> GetByIdAsync(string tenantId, Guid workflowId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            return ByIdResult;
        }

        public async Task<List<(WorkflowRow Workflow, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            return ListWithDisplayIdsResult;
        }

        public async Task<(int TotalCount, List<(WorkflowRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            string tenantId, int offset, int limit, string? statusFilter, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            return ListWithDisplayIdsPageResult;
        }

        public async Task AddWorkflowAndSnapshotAsync(WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct)
        {
            Added.Add((workflow, snapshot));
            await Task.Yield(); // async boundary for coverage
        }

        public async Task AddWorkflowAndSnapshotAsync(CoreDbContext db, WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct)
        {
            Added.Add((workflow, snapshot));
            await Task.Yield(); // async boundary for coverage
        }

        public async Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(Guid workflowId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            return SnapshotByWorkflowId;
        }

        public async Task UpdateWorkflowAndSnapshotAsync(Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct)
        {
            Updates.Add((workflowId, status, cancelRequested, graphJson));
            await Task.Yield(); // async boundary for coverage
        }

        public async Task UpdateWorkflowAndSnapshotAsync(CoreDbContext db, Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct)
        {
            Updates.Add((workflowId, status, cancelRequested, graphJson));
            await Task.Yield(); // async boundary for coverage
        }
    }

    private sealed class FakeEventStoreRepository : IEventStoreRepository
    {
        public List<(EventStoreEventType Type, Guid WorkflowId, string? Payload)> Appended { get; } = new();
        public List<EventStoreRow> AfterSeqItems { get; set; } = new();
        public bool AfterSeqHasMore { get; set; }
        public long MaxSeq { get; set; }

        /// <summary>設定時に追記処理で例外を投げて巻き戻し分岐を通す。</summary>
        public Exception? ThrowFromAppendWithDb { get; set; }

        public Task AppendAsync(Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task AppendAsync(CoreDbContext db, Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default)
        {
            if (ThrowFromAppendWithDb is { } ex)
                throw ex;

            Appended.Add((eventType, workflowId, payloadJson));
            await Task.Yield(); // async boundary for coverage
        }

        public async Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(Guid workflowId, long afterSeq, int limit, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return ((IReadOnlyList<EventStoreRow>)AfterSeqItems, AfterSeqHasMore);
        }

        public async Task<long> GetMaxSeqAsync(Guid workflowId, CancellationToken ct = default)
        {
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
        var cached = new WorkflowResponse
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
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "idem",
                ResponseBody = cachedJson,
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/workflows", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService();
        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("x"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var workflowRepo = new FakeWorkflowRepository();
        var definitionsRepo = new FakeDefinitionsRepoStub();
        var dedupRepoRepo = dedupRepo;
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var dbFactory = sqlite.Factory;

        var sut = new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepoRepo,
            eventStore,
            dbFactory);

        using var inputDoc = JsonDocument.Parse("{\"a\":1}");
        var request = new StartWorkflowRequest { DefinitionId = "def-1", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            tenantId: "t1",
            request: request,
            idempotencyKey: "idem",
            method: "POST",
            path: "/v1/workflows",
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
                DedupKey = "t1|POST /v1/workflows:idem:deadbeef",
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "idem",
                RequestHash = "deadbeef",
                StatusCode = StatusCodes.Status201Created,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d-new", Endpoint = "POST /v1/workflows", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService();
        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("x"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var workflowRepo = new FakeWorkflowRepository();
        var definitionsRepo = new FakeDefinitionsRepoStub();
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            sqlite.Factory);

        using var inputDoc = JsonDocument.Parse("{}");
        var request = new StartWorkflowRequest { DefinitionId = "def-1", Input = inputDoc.RootElement };

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => sut.StartAsync(
            tenantId: "t1",
            request: request,
            idempotencyKey: "idem",
            method: "POST",
            path: "/v1/workflows",
            CancellationToken.None));

        Assert.False(engine.StartCalled);
    }

    private sealed class FakeDefinitionsRepoStub : IDefinitionRepository
    {
        public Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct) => Task.CompletedTask;
        public Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct) => Task.FromResult<WorkflowDefinitionRow?>(null);
        public Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct) => Task.FromResult(new List<(WorkflowDefinitionRow, string?)>());
        public Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(string tenantId, int offset, int limit, string? nameContains, CancellationToken ct) =>
            Task.FromResult((0, new List<(WorkflowDefinitionRow, string?)>()));
    }

    /// <summary>冪等一致でもキャッシュ本文が壊れているとき通常起動して要求要約を保存する。</summary>
    [Fact]
    public async Task StartAsync_WhenDedupHitButCachedInvalid_ProceedsAndSavesDedupRequestHash()
    {
        // Arrange
        var defUuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var workflowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var engineSnapshot = new WorkflowSnapshot
        {
            WorkflowId = workflowId.ToString(),
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
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "idem",
                ResponseBody = "{invalid-json",
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/workflows", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            ResolveResultWorkflow = workflowId,
            AllocateResultWorkflow = "WF-DISP-2",
            GetDisplayIdResult = null
        };

        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(workflowId);
        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = engineSnapshot,
            GraphJsonToReturn = "{\"nodes\":[{\"nodeId\":\"n1\",\"stateName\":\"S\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}"
        };

        var workflowRepo = new FakeWorkflowRepository();

        var definitionsRepo = new FakeDefinitionsRepoStub2(defUuid);

        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            sqlite.Factory);

        using var inputDoc = JsonDocument.Parse("{\"x\":true}");
        var request = new StartWorkflowRequest { DefinitionId = "def-2", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            tenantId: "t1",
            request: request,
            idempotencyKey: "idem",
            method: "POST",
            path: "/v1/workflows",
            CancellationToken.None);

        // Assert
        // Assert
        Assert.True(engine.StartCalled);
        Assert.Single(workflowRepo.Added);
        Assert.Single(dedupRepo.SavedRows);
        Assert.Equal("WF-DISP-2", res.DisplayId);
        Assert.Equal(workflowId, res.ResourceId);
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
        var workflowId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var engineSnapshot = new WorkflowSnapshot
        {
            WorkflowId = workflowId.ToString(),
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
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "idem",
                ResponseBody = null,
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        var dedupKey = new CommandDedupKey { DedupKey = "d1", Endpoint = "POST /v1/workflows", IdempotencyKey = "idem" };
        var dedupService = new FakeCommandDedupService(dedupKey);

        var display = new FakeDisplayIdService
        {
            ResolveResultDefinition = defUuid,
            ResolveResultWorkflow = workflowId,
            AllocateResultWorkflow = "WF-DISP-3",
            GetDisplayIdResult = null
        };

        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(workflowId);
        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = engineSnapshot,
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var workflowRepo = new FakeWorkflowRepository();
        var definitionsRepo = new FakeDefinitionsRepoStub2(defUuid);
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            sqlite.Factory);

        using var inputDoc = JsonDocument.Parse("{\"x\":true}");
        var request = new StartWorkflowRequest { DefinitionId = "def-2", Input = inputDoc.RootElement };

        // Act
        var res = await sut.StartAsync(
            tenantId: "t1",
            request: request,
            idempotencyKey: "idem",
            method: "POST",
            path: "/v1/workflows",
            CancellationToken.None);

        // Assert
        Assert.True(engine.StartCalled);
        Assert.Single(workflowRepo.Added);
        Assert.Single(dedupRepo.SavedRows);
        Assert.Equal("WF-DISP-3", res.DisplayId);
        Assert.Equal(workflowId, res.ResourceId);
        Assert.Equal("Completed", res.Status);
        Assert.False(string.IsNullOrWhiteSpace(dedupService.LastRequestHash));
        Assert.Equal(dedupService.LastRequestHash, dedupRepo.SavedRows[0].RequestHash);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowStarted, eventStore.Appended[0].Type);
    }

    private sealed class FakeDefinitionsRepoStub2 : IDefinitionRepository
    {
        private readonly Guid _defUuid;
        public FakeDefinitionsRepoStub2(Guid defUuid) => _defUuid = defUuid;
        public Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct) => Task.CompletedTask;
        public Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct) =>
            Task.FromResult(definitionId == _defUuid
                ? new WorkflowDefinitionRow
                {
                    DefinitionId = _defUuid,
                    TenantId = tenantId,
                    Name = "def",
                    SourceYaml = "yaml",
                    CompiledJson = "{}",
                    CreatedAt = DateTime.UtcNow
                }
                : null);

        public Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct) =>
            Task.FromResult(new List<(WorkflowDefinitionRow, string?)>());

        public Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            string tenantId, int offset, int limit, string? nameContains, CancellationToken ct) =>
            Task.FromResult((0, new List<(WorkflowDefinitionRow, string?)>()));
    }

    /// <summary>定義を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task StartAsync_WhenDefinitionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dedupService = new FakeCommandDedupService(null);
        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var display = new FakeDisplayIdService { ResolveResultDefinition = null };
        var engine = new FakeWorkflowEngine();
        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var workflowRepo = new FakeWorkflowRepository();
        var definitionsRepo = new FakeDefinitionsRepoStub();
        var eventStore = new FakeEventStoreRepository();

        using var sqlite = new SqliteTestDatabase();
        var sut = new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            sqlite.Factory);

        var request = new StartWorkflowRequest { DefinitionId = "missing" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartAsync("t1", request, idempotencyKey: null, method: "POST", path: "/v1/workflows", CancellationToken.None));
    }

    /// <summary>連番指定が一未満のとき引数例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAtSeqAsync_WhenAtSeqLessThan1_ThrowsArgumentException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var sut = MakeSut(
            sqlite,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetWorkflowViewAtSeqAsync("t1", idOrUuid: "x", atSeq: 0, CancellationToken.None));
    }

    private static WorkflowService MakeSut(SqliteTestDatabase sqlite, out FakeWorkflowRepository workflowRepo)
    {
        workflowRepo = new FakeWorkflowRepository();

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = Guid.NewGuid(), ResolveResultDefinition = Guid.NewGuid() };
        var compiler = new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var dedupService = new FakeCommandDedupService(null);
        var definitionsRepo = new FakeDefinitionsRepoStub();
        var dedupRepo = new FakeCommandDedupRepository();
        var eventStore = new FakeEventStoreRepository();

        return new WorkflowService(
            engine,
            display,
            compiler,
            idGen,
            dedupService,
            workflowRepo,
            definitionsRepo,
            dedupRepo,
            eventStore,
            sqlite.Factory);
    }

    /// <summary>最大連番が零でも連番一の表示を返す。</summary>
    [Fact]
    public async Task GetWorkflowViewAtSeqAsync_WhenMaxSeqIs0_ReturnsWorkflowView()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = new WorkflowService(
            engine,
            display,
            new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            new FakeCommandDedupService(null),
            workflowRepo,
            new FakeDefinitionsRepoStub(),
            new FakeCommandDedupRepository(),
            new FakeEventStoreRepository { MaxSeq = 0 },
            sqlite.Factory);

        // Act
        var view = await sut.GetWorkflowViewAtSeqAsync("t1", idOrUuid: "display-or-uuid", atSeq: 1, CancellationToken.None);

        // Assert
        Assert.Equal("Running", view.Status);
        Assert.Equal(workflowId.ToString("D"), view.ResourceId);
        Assert.Equal(2, view.Nodes.Count);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal("S2", view.Nodes[1].NodeType);
        Assert.Equal("FAILED", view.Nodes[1].Status);
        Assert.Equal(defId.ToString("D"), view.GraphId);
    }

    /// <summary>実行識別子の解決に失敗したとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAtSeqAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();

        var display = new FakeDisplayIdService { ResolveResultWorkflow = null };
        var sut = new WorkflowService(
            engine: new FakeWorkflowEngine(),
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: new FakeWorkflowRepository(),
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: new FakeEventStoreRepository(),
            dbFactory: sqlite.Factory);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetWorkflowViewAtSeqAsync("t1", idOrUuid: "x", atSeq: 1, CancellationToken.None));
    }

    /// <summary>指定連番が最大連番を超えるとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAtSeqAsync_WhenAtSeqOutOfRange_ThrowsNotFoundException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = new WorkflowService(
            engine: engine,
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: workflowRepo,
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: new FakeEventStoreRepository { MaxSeq = 5 },
            dbFactory: sqlite.Factory);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetWorkflowViewAtSeqAsync("t1", idOrUuid: "display-or-uuid", atSeq: 6, CancellationToken.None));
    }

    /// <summary>指定連番が範囲内で最大連番が正のとき表示を返す。</summary>
    [Fact]
    public async Task GetWorkflowViewAtSeqAsync_WhenAtSeqWithinRangeAndMaxSeqNonZero_ReturnsWorkflowView()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson =
                    "{\"nodes\":[" +
                    "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
                    "{\"nodeId\":\"n2\",\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Failed\"}" +
                    "]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = new WorkflowService(
            engine: engine,
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: workflowRepo,
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: new FakeEventStoreRepository { MaxSeq = 5 },
            dbFactory: sqlite.Factory);

        // Act
        var view = await sut.GetWorkflowViewAtSeqAsync("t1", idOrUuid: "display-or-uuid", atSeq: 2, CancellationToken.None);

        // Assert
        Assert.Equal("Running", view.Status);
        Assert.Equal(workflowId.ToString("D"), view.ResourceId);
        Assert.Equal(2, view.Nodes.Count);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.Equal("S2", view.Nodes[1].NodeType);
        Assert.Equal("FAILED", view.Nodes[1].Status);
        Assert.Equal(defId.ToString("D"), view.GraphId);
    }

    /// <summary>イベント記録を時系列へ変換し更新イベントに差分を付ける。</summary>
    [Fact]
    public async Task ListEventsAsync_MapsTimelineEventsAndPublishedGraphPatch()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var graphJson =
            "{\"nodes\":[" +
            "{\"nodeId\":\"a\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
            "{\"nodeId\":\"b\",\"stateName\":\"Wait\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
            "]}";

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            ResolveResultDefinition = workflowRepo.ByIdResult!.DefinitionId,
            GetDisplayIdResult = null
        };

        var afterSeqItems = new List<EventStoreRow>
        {
            new EventStoreRow { WorkflowId = workflowId, Seq = 1, Type = EventStoreEventType.WorkflowStarted.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { WorkflowId = workflowId, Seq = 2, Type = EventStoreEventType.WorkflowCancelled.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { WorkflowId = workflowId, Seq = 3, Type = EventStoreEventType.EventPublished.ToPersistedString(), OccurredAt = DateTime.UtcNow },
            new EventStoreRow { WorkflowId = workflowId, Seq = 4, Type = "Unknown", OccurredAt = DateTime.UtcNow }
        };

        var eventStore = new FakeEventStoreRepository
        {
            AfterSeqItems = afterSeqItems,
            AfterSeqHasMore = false
        };

        using var sqlite = new SqliteTestDatabase();
        var sut = new WorkflowService(
            engine: new FakeWorkflowEngine(),
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: workflowRepo,
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: eventStore,
            dbFactory: sqlite.Factory);

        // Act
        var res = await sut.ListEventsAsync("t1", idOrUuid: "idOrUuid", afterSeq: 0, limit: 10, CancellationToken.None);

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
            sut.ResumeNodeAsync("t1", idOrUuid: "workflow", nodeId: "  ", resumeKey: "Approve", idempotencyKey: null, method: "POST", path: "/v1/workflows", CancellationToken.None));
    }

    /// <summary>取消要求で冪等一致かつ有効行があるとき副作用なく即時終了する。</summary>
    [Fact]
    public async Task CancelAsync_WhenDedupHitAndExistingNotNull_ReturnsEarly_WithoutSideEffects()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows/cancel",
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

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };
        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync("t1", idOrUuid: "X", idempotencyKey: "idem", method: "POST", path: "/v1/workflows/cancel", CancellationToken.None);

        // Assert
        Assert.False(engine.CancelCalled);
        Assert.Empty(workflowRepo.Updates);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>取消時に実行識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task CancelAsync_WhenWorkflowResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = null };
        var workflowRepo = new FakeWorkflowRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CancelAsync("t1", idOrUuid: "X", idempotencyKey: null, method: "POST", path: "/v1/workflows/cancel", CancellationToken.None));

        Assert.False(engine.CancelCalled);
    }

    /// <summary>解決後に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task CancelAsync_WhenWorkflowMissingAfterResolve_ThrowsNotFoundException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };
        var workflowRepo = new FakeWorkflowRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CancelAsync("t1", idOrUuid: "X", idempotencyKey: null, method: "POST", path: "/v1/workflows/cancel", CancellationToken.None));

        Assert.False(engine.CancelCalled);
    }

    /// <summary>取消処理が通ると投影更新と追記保存まで実施する。</summary>
    [Fact]
    public async Task CancelAsync_WhenProceeding_UpdatesWorkflowAndSnapshot_AppendsCancelledEvent_AndSavesDedupRow()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows/cancel",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
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
            ResolveResultWorkflow = workflowId
        };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync(tenantId, idOrUuid: "X", idempotencyKey: "idem", method: "POST", path: "/v1/workflows/cancel", CancellationToken.None);

        // Assert
        Assert.True(engine.CancelCalled);
        Assert.Single(workflowRepo.Updates);
        Assert.Equal(workflowId, workflowRepo.Updates[0].WorkflowId);
        Assert.Equal("Cancelled", workflowRepo.Updates[0].Status);
        Assert.True(workflowRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, workflowRepo.Updates[0].GraphJson);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.WorkflowCancelled, eventStore.Appended[0].Type);
        Assert.Equal(workflowId, eventStore.Appended[0].WorkflowId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(tenantId, payloadDoc.RootElement.GetProperty("tenantId").GetString());

        Assert.Single(dedupRepo.SavedRows);
        Assert.Null(dedupRepo.SavedRows[0].RequestHash);
        Assert.Equal(StatusCodes.Status204NoContent, dedupRepo.SavedRows[0].StatusCode);
        Assert.Null(dedupRepo.SavedRows[0].ResponseBody);
        Assert.Equal(dedupKey.DedupKey, dedupRepo.SavedRows[0].DedupKey);
        Assert.Equal(dedupKey.Endpoint, dedupRepo.SavedRows[0].Endpoint);
        Assert.Equal(dedupKey.IdempotencyKey, dedupRepo.SavedRows[0].IdempotencyKey);
    }

    /// <summary>実行機構のスナップショットが空値のとき不明状態と空の取消要求で更新する。</summary>
    [Fact]
    public async Task CancelAsync_WhenEngineSnapshotIsNull_UsesUnknownStatus_AndNullCancelRequested()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows/cancel",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = null,
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        await sut.CancelAsync(tenantId, idOrUuid: "X", idempotencyKey: "idem", method: "POST", path: "/v1/workflows/cancel", CancellationToken.None);

        // Assert
        Assert.Single(workflowRepo.Updates);
        Assert.Equal("Unknown", workflowRepo.Updates[0].Status);
        Assert.Null(workflowRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, workflowRepo.Updates[0].GraphJson);
    }

    /// <summary>イベント追記に失敗したとき巻き戻して例外を再送出する。</summary>
    [Fact]
    public async Task CancelAsync_WhenEventStoreAppendThrows_RollsBackAndRethrows()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CancelAsync(tenantId, idOrUuid: "X", idempotencyKey: null, method: "POST", path: "/v1/workflows/cancel", CancellationToken.None));

        // Assert
        Assert.Equal("append failed", ex.Message);
        Assert.True(engine.CancelCalled);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>非公開投影更新処理が実行行とスナップショットを更新する。</summary>
    [Fact]
    public async Task UpdateProjectionAsync_PrivateMethod_UpdatesWorkflowAndSnapshot()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        using var sqlite = new SqliteTestDatabase();

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService();
        var workflowRepo = new FakeWorkflowRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = new WorkflowService(
            engine: engine,
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: workflowRepo,
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: eventStore,
            dbFactory: sqlite.Factory);

        var method = typeof(WorkflowService).GetMethod(
            "UpdateProjectionAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);

        // Act
        var task = (Task?)method!.Invoke(sut, new object[] { workflowId, CancellationToken.None });
        Assert.NotNull(task);

        await task!;

        // Assert
        Assert.Single(workflowRepo.Updates);
        Assert.Equal("Cancelled", workflowRepo.Updates[0].Status);
        Assert.True(workflowRepo.Updates[0].CancelRequested.GetValueOrDefault());
        Assert.Equal(engine.GraphJsonToReturn, workflowRepo.Updates[0].GraphJson);
    }

    /// <summary>イベント公開で冪等一致かつ有効行があるとき副作用なく即時終了する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenDedupHitAndExistingNotNull_ReturnsEarly_WithoutSideEffects()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows/events",
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

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };
        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = "t1",
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        await sut.PublishEventAsync(tenantId: "t1", idOrUuid: "X", eventName: "Approve", idempotencyKey: "idem", method: "POST", path: "/v1/workflows/events", CancellationToken.None);

        // Assert
        Assert.Null(engine.PublishEventLastName);
        Assert.Empty(workflowRepo.Updates);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>解決後に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenWorkflowMissingAfterResolve_ThrowsNotFoundException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };
        var workflowRepo = new FakeWorkflowRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.PublishEventAsync(tenantId: "t1", idOrUuid: "X", eventName: "Approve", idempotencyKey: null, method: "POST", path: "/v1/workflows/events", CancellationToken.None));

        Assert.Null(engine.PublishEventLastWorkflowId);
        Assert.Null(engine.PublishEventLastName);
    }

    /// <summary>イベント公開が通ると通知と投影更新と追記保存まで実施する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenProceeding_UpdatesWorkflowAndSnapshot_AppendsEventPublished_AndSavesDedupRow()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupKey = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows/events",
            IdempotencyKey = "idem"
        };

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
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
            ResolveResultWorkflow = workflowId
        };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        const string eventName = "Approve";

        // Act
        await sut.PublishEventAsync(tenantId: tenantId, idOrUuid: "X", eventName: eventName, idempotencyKey: "idem", method: "POST", path: "/v1/workflows/events", CancellationToken.None);

        // Assert
        Assert.Equal(workflowId.ToString(), engine.PublishEventLastWorkflowId);
        Assert.Equal(eventName, engine.PublishEventLastName);

        Assert.Single(workflowRepo.Updates);
        Assert.Equal(workflowId, workflowRepo.Updates[0].WorkflowId);
        Assert.Equal("Failed", workflowRepo.Updates[0].Status);
        Assert.False(workflowRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, workflowRepo.Updates[0].GraphJson);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(workflowId, eventStore.Appended[0].WorkflowId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(tenantId, payloadDoc.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(eventName, payloadDoc.RootElement.GetProperty("name").GetString());

        Assert.Single(dedupRepo.SavedRows);
        Assert.Null(dedupRepo.SavedRows[0].RequestHash);
        Assert.Equal(StatusCodes.Status204NoContent, dedupRepo.SavedRows[0].StatusCode);
        Assert.Null(dedupRepo.SavedRows[0].ResponseBody);
        Assert.Equal(dedupKey.DedupKey, dedupRepo.SavedRows[0].DedupKey);
        Assert.Equal(dedupKey.Endpoint, dedupRepo.SavedRows[0].Endpoint);
        Assert.Equal(dedupKey.IdempotencyKey, dedupRepo.SavedRows[0].IdempotencyKey);
    }

    /// <summary>イベント公開時の追記失敗で巻き戻して例外を再送出する。</summary>
    [Fact]
    public async Task PublishEventAsync_WhenEventStoreAppendThrows_RollsBackAndRethrows()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var dedupRepo = new FakeCommandDedupRepository { NextFindValid = null };

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        const string eventName = "Approve";

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.PublishEventAsync(tenantId: tenantId, idOrUuid: "X", eventName: eventName, idempotencyKey: null, method: "POST", path: "/v1/workflows/events", CancellationToken.None));

        // Assert
        Assert.Equal("append failed", ex.Message);
        Assert.Equal(workflowId.ToString(), engine.PublishEventLastWorkflowId);
        Assert.Equal(eventName, engine.PublishEventLastName);
        Assert.Empty(eventStore.Appended);
        Assert.Empty(dedupRepo.SavedRows);
    }

    /// <summary>応答取得で実行識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowResponseAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = null };
        var workflowRepo = new FakeWorkflowRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetWorkflowResponseAsync("t1", idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>応答取得で実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowResponseAsync_WhenWorkflowMissing_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = uuid };
        var workflowRepo = new FakeWorkflowRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetWorkflowResponseAsync("t1", idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>表示用識別子がないとき識別子文字列へフォールバックする。</summary>
    [Fact]
    public async Task GetWorkflowResponseAsync_WhenDisplayIdMissing_FallsBackToUuidString()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var uuid = Guid.NewGuid();
        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = uuid,
                TenantId = "t1",
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
            ResolveResultWorkflow = uuid,
            GetDisplayIdResult = null
        };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act
        var res = await sut.GetWorkflowResponseAsync("t1", idOrUuid: "X", CancellationToken.None);

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
        var engine = new FakeWorkflowEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = uuid };
        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = uuid,
                TenantId = "t1",
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
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync("t1", idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>グラフ取得時に識別子を解決できないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = null };
        var workflowRepo = new FakeWorkflowRepository();
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync("t1", idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>グラフ取得時に実行行がないとき未検出例外を投げる。</summary>
    [Fact]
    public async Task GetGraphJsonAsync_WhenWorkflowMissing_ThrowsNotFoundException()
    {
        // Arrange
        var engine = new FakeWorkflowEngine();
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService { ResolveResultWorkflow = uuid };
        var workflowRepo = new FakeWorkflowRepository { ByIdResult = null };
        var eventStore = new FakeEventStoreRepository();

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: engine,
            display: display,
            workflowRepo: workflowRepo,
            eventStore: eventStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetGraphJsonAsync("t1", idOrUuid: "X", CancellationToken.None));
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
            sut.ResumeNodeAsync("t1", idOrUuid: "workflow", nodeId: "node-1", resumeKey: null, idempotencyKey: null, method: "POST", path: "/v1/workflows", CancellationToken.None));
    }

    /// <summary>有効なノード識別子と再開キーで公開と投影更新を実施する。</summary>
    [Fact]
    public async Task ResumeNodeAsync_WhenValidNodeAndResumeKey_PublishesEventAndUpdatesWorkflow()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var nodeId = "node-1";
        var resumeKey = "Approve";

        using var sqlite = new SqliteTestDatabase();

        var engine = new FakeWorkflowEngine
        {
            SnapshotToReturn = new WorkflowSnapshot
            {
                WorkflowId = workflowId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = true,
                IsFailed = false
            },
            GraphJsonToReturn = "{\"nodes\":[]}"
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var eventStore = new FakeEventStoreRepository();

        var sut = new WorkflowService(
            engine: engine,
            displayIds: display,
            compiler: new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            idGenerator: new FixedIdGenerator(Guid.NewGuid()),
            dedupService: new FakeCommandDedupService(null),
            workflows: workflowRepo,
            definitions: new FakeDefinitionsRepoStub(),
            dedup: new FakeCommandDedupRepository(),
            eventStore: eventStore,
            dbFactory: sqlite.Factory);

        // Act
        await sut.ResumeNodeAsync(tenantId, idOrUuid: "X", nodeId: nodeId, resumeKey: resumeKey, idempotencyKey: null, method: "POST", path: "/v1/workflows", CancellationToken.None);

        // Assert
        Assert.Equal(workflowId.ToString(), engine.PublishEventLastWorkflowId);
        Assert.Equal(resumeKey, engine.PublishEventLastName);

        Assert.Single(eventStore.Appended);
        Assert.Equal(EventStoreEventType.EventPublished, eventStore.Appended[0].Type);
        Assert.Equal(workflowId, eventStore.Appended[0].WorkflowId);

        var payload = eventStore.Appended[0].Payload;
        Assert.NotNull(payload);
        using var payloadDoc = JsonDocument.Parse(payload);
        Assert.Equal(tenantId, payloadDoc.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(resumeKey, payloadDoc.RootElement.GetProperty("name").GetString());

        Assert.Single(workflowRepo.Updates);
        Assert.Equal("Cancelled", workflowRepo.Updates[0].Status);
        Assert.True(workflowRepo.Updates[0].CancelRequested);
        Assert.Equal(engine.GraphJsonToReturn, workflowRepo.Updates[0].GraphJson);
    }

    /// <summary>一覧で表示用識別子が空値の行は識別子文字列を表示値に使う。</summary>
    [Fact]
    public async Task ListAsync_WhenDisplayIdMissing_FallsBackToWorkflowIdString()
    {
        // Arrange
        var tenantId = "t1";
        var w1 = new WorkflowRow
        {
            WorkflowId = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        };
        var w2 = new WorkflowRow
        {
            WorkflowId = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Completed",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = true,
            RestartLost = true
        };

        var workflowRepo = new FakeWorkflowRepository
        {
            ListWithDisplayIdsResult = new List<(WorkflowRow Workflow, string? DisplayId)>
            {
                (w1, null),
                (w2, "WF-DISP-2")
            }
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: new FakeDisplayIdService(),
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var res = await sut.ListAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.Equal(2, res.Count);
        Assert.Equal(w1.WorkflowId.ToString("D"), res[0].DisplayId);
        Assert.Equal(w1.WorkflowId, res[0].ResourceId);
        Assert.Equal("Running", res[0].Status);
        Assert.Equal(w2.WorkflowId, res[1].ResourceId);
        Assert.Equal("WF-DISP-2", res[1].DisplayId);
        Assert.True(res[1].CancelRequested);
        Assert.True(res[1].RestartLost);
    }

    /// <summary>ページングで総件数と件数上限から続き有無を切り替える。</summary>
    [Fact]
    public async Task ListPagedAsync_HasMore_AndHasNotMore()
    {
        // Arrange
        var tenantId = "t1";
        var w1 = new WorkflowRow
        {
            WorkflowId = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var w2 = new WorkflowRow
        {
            WorkflowId = Guid.NewGuid(),
            TenantId = tenantId,
            DefinitionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var workflowRepo = new FakeWorkflowRepository
        {
            ListWithDisplayIdsPageResult = (3, new List<(WorkflowRow Workflow, string? DisplayId)> { (w1, null), (w2, null) })
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: new FakeDisplayIdService(),
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var page = await sut.ListPagedAsync(tenantId, offset: 0, limit: 2, status: null, CancellationToken.None);

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Items.Count);

        workflowRepo.ListWithDisplayIdsPageResult = (2, new List<(WorkflowRow Workflow, string? DisplayId)> { (w1, null), (w2, null) });

        // Act
        var page2 = await sut.ListPagedAsync(tenantId, offset: 0, limit: 2, status: null, CancellationToken.None);

        // Assert
        Assert.Equal(2, page2.TotalCount);
        Assert.False(page2.HasMore);
    }

    /// <summary>グラフ文字列からノード種別と状態と補完識別子を組み立てる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_Success_BuildsNodesAndGraphIdFallbacks()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var graphJson =
            "{\"nodes\":[" +
            "{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}," +
            "{\"nodeId\":null,\"stateName\":\"S2\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Cancelled\"}" +
            ",{\"nodeId\":\"n3\",\"stateName\":\"S3\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"SomeOtherFact\"}," +
            "{\"nodeId\":\"n4\",\"stateName\":\"S4\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Completed\"}," +
            "{\"nodeId\":\"n5\",\"stateName\":\"S5\",\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":\"2020-01-01T00:00:00Z\",\"fact\":\"Joined\"}" +
            "]}";

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            GetDisplayIdResult = null
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal(workflowId.ToString("D"), view.ResourceId);
        Assert.Equal(workflowId.ToString("D"), view.DisplayId); // displayId fallback
        Assert.Equal(defId.ToString("D"), view.GraphId); // graphId fallback

        Assert.Equal(5, view.Nodes.Count);
        Assert.Equal("n1", view.Nodes[0].NodeId);
        Assert.Equal("Task", view.Nodes[0].NodeType);
        Assert.Equal("RUNNING", view.Nodes[0].Status);
        Assert.False(view.Nodes[0].CanceledByExecution);

        Assert.Equal(string.Empty, view.Nodes[1].NodeId);
        Assert.Equal("S2", view.Nodes[1].NodeType);
        Assert.Equal("CANCELED", view.Nodes[1].Status);
        Assert.True(view.Nodes[1].CanceledByExecution);

        Assert.Equal("n3", view.Nodes[2].NodeId);
        Assert.Equal("S3", view.Nodes[2].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[2].Status); // default branch of MapNodeStatus
        Assert.False(view.Nodes[2].CanceledByExecution);

        Assert.Equal("n4", view.Nodes[3].NodeId);
        Assert.Equal("S4", view.Nodes[3].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[3].Status); // Completed -> SUCCEEDED
        Assert.False(view.Nodes[3].CanceledByExecution);

        Assert.Equal("n5", view.Nodes[4].NodeId);
        Assert.Equal("S5", view.Nodes[4].NodeType);
        Assert.Equal("SUCCEEDED", view.Nodes[4].Status); // Joined -> SUCCEEDED
        Assert.False(view.Nodes[4].CanceledByExecution);
    }

    /// <summary>グラフ文字列が空白のみのときノード一覧は空になる。</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task GetWorkflowViewAsync_WhenGraphJsonIsWhitespace_ReturnsEmptyNodes(string graphJson)
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>グラフ文字列が不正なとき例外にせずノード一覧を空にする。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenGraphJsonInvalid_ReturnsEmptyNodes()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = "{not-json",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>ノード属性が空値のときノード一覧は空になる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenNodesPropertyIsNull_ReturnsEmptyNodes()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = "{\"nodes\":null}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>ノード属性が空配列のときノード一覧は空になる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenNodesArrayEmpty_ReturnsEmptyNodes()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = "{\"nodes\":[]}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId, GetDisplayIdResult = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act
        var view = await sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Empty(view.Nodes);
    }

    /// <summary>スナップショット行がないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenSnapshotMissing_ThrowsNotFoundException()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            },
            SnapshotByWorkflowId = null
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = workflowId };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>実行識別子を解決できないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenResolveReturnsNull_ThrowsNotFoundException()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository
        {
            ByIdResult = new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            }
        };

        var display = new FakeDisplayIdService { ResolveResultWorkflow = null };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None));
    }

    /// <summary>解決後に実行行がないとき表示取得で未検出例外を投げる。</summary>
    [Fact]
    public async Task GetWorkflowViewAsync_WhenWorkflowMissing_ThrowsNotFoundException()
    {
        // Arrange
        var tenantId = "t1";
        var workflowId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        var workflowRepo = new FakeWorkflowRepository { ByIdResult = null };

        var display = new FakeDisplayIdService
        {
            ResolveResultWorkflow = workflowId,
            ResolveResultDefinition = defId,
            GetDisplayIdResult = null
        };

        var sut = MakeSut(
            dedupService: new FakeCommandDedupService(null),
            dedupRepo: new FakeCommandDedupRepository(),
            engine: new FakeWorkflowEngine(),
            display: display,
            workflowRepo: workflowRepo,
            eventStore: new FakeEventStoreRepository());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetWorkflowViewAsync(tenantId, idOrUuid: "X", CancellationToken.None));
    }

    private static WorkflowService MakeSut(
        FakeCommandDedupService dedupService,
        FakeCommandDedupRepository dedupRepo,
        FakeWorkflowEngine engine,
        FakeDisplayIdService display,
        FakeWorkflowRepository workflowRepo,
        FakeEventStoreRepository eventStore)
    {
        var sqlite = new SqliteTestDatabase();

        return new WorkflowService(
            engine,
            display,
            new FakeDefinitionCompilerService((DummyCompiledDefinition("def"), "{}")),
            new FixedIdGenerator(Guid.NewGuid()),
            dedupService,
            workflowRepo,
            new FakeDefinitionsRepoStub(),
            dedupRepo,
            eventStore,
            sqlite.Factory);
    }
}


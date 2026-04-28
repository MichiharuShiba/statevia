using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Services;

public sealed class DefinitionServiceTests
{
    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Guid _id;
        public FixedIdGenerator(Guid id) => _id = id;
        public Guid NewGuid() => _id;
    }

    private sealed class StubCompiler : IDefinitionCompilerService
    {
        private readonly string _compiledJson;
        public StubCompiler(string compiledJson) => _compiledJson = compiledJson;
        public (Statevia.Core.Engine.Abstractions.CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml)
        {
            // DefinitionService は compiled オブジェクトを使わず、CompiledJson だけを保存する。
            var dummyFactory = new DummyExecutorFactory();
            return (new Statevia.Core.Engine.Abstractions.CompiledWorkflowDefinition
            {
                Name = name,
                Transitions = new Dictionary<string, IReadOnlyDictionary<string, Statevia.Core.Engine.Abstractions.TransitionTarget>>(),
                ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
                JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
                WaitTable = new Dictionary<string, string>(),
                InitialState = "A",
                StateExecutorFactory = dummyFactory
            }, _compiledJson);
        }

        private sealed class DummyExecutorFactory : Statevia.Core.Engine.Abstractions.IStateExecutorFactory
        {
            public Statevia.Core.Engine.Abstractions.IStateExecutor? GetExecutor(string stateName) => null;
        }
    }

    private sealed class ThrowingCompiler : IDefinitionCompilerService
    {
        private readonly Exception _exception;

        public ThrowingCompiler(Exception exception)
        {
            _exception = exception;
        }

        public (Statevia.Core.Engine.Abstractions.CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml)
            => throw _exception;
    }

    private sealed class StubDisplayIdService : IDisplayIdService
    {
        public string? AllocateValue { get; init; }
        public Dictionary<string, Guid> ResolveMap { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string Kind, string IdOrUuid), string?> DisplayMap { get; } = new();

        public Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default)
        {
            return Task.FromResult(AllocateValue ?? "DISP-1");
        }

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            return Task.FromResult<Guid?>(ResolveMap.TryGetValue($"{kind}|{idOrUuid}", out var v) ? v : null);
        }

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            DisplayMap.TryGetValue((kind, idOrUuid), out var v);
            return Task.FromResult(v);
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default)
        {
            var dict = new Dictionary<Guid, string>();
            foreach (var id in resourceIds)
                if (DisplayMap.TryGetValue((kind, id.ToString()), out var v) && v is not null)
                    dict[id] = v;
            return Task.FromResult((IReadOnlyDictionary<Guid, string>)dict);
        }
    }

    /// <summary>
    /// 定義を保存して表示用識別子を返す。
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsWorkflowDefinition_AndReturnsDisplayId()
    {
        // Arrange
        var defGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        // Act
        var display = new StubDisplayIdService { AllocateValue = "DEF-DISP" };
        var compiler = new StubCompiler(compiledJson: "{\"nodes\":[]}");
        var idGen = new FixedIdGenerator(defGuid);

        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        var request = new CreateDefinitionRequest { Name = "my-def", Yaml = "workflow:\n  name: x\nstates: {}" };

        // Assert
        var res = await sut.CreateAsync("t1", request, CancellationToken.None);
        Assert.Equal("DEF-DISP", res.DisplayId);
        Assert.Equal(defGuid, res.ResourceId);

        await using var verifyDb = new CoreDbContext(inDb.Options);
        var row = await verifyDb.WorkflowDefinitions.FirstOrDefaultAsync(x => x.DefinitionId == defGuid);
        Assert.NotNull(row);
        Assert.Equal("my-def", row!.Name);
        Assert.Equal("{\"nodes\":[]}", row.CompiledJson);
    }

    /// <summary>
    /// 表示用識別子がない行は識別子文字列を返す。
    /// </summary>
    [Fact]
    public async Task ListAsync_WhenDisplayIdMissing_FallsBackToDefinitionIdString()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        // Act
        // display_ids(kind=definition) を片方だけ入れて、もう片方は DisplayId が null になる状況を作る。
        var def1 = Guid.NewGuid();
        var def2 = Guid.NewGuid();
        var t1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(inDb.Options))
        {
            ctx.WorkflowDefinitions.AddRange(
                new WorkflowDefinitionRow { DefinitionId = def1, TenantId = "t1", Name = "A", SourceYaml = "x", CompiledJson = "{}", CreatedAt = t1 },
                new WorkflowDefinitionRow { DefinitionId = def2, TenantId = "t1", Name = "B", SourceYaml = "x", CompiledJson = "{}", CreatedAt = t2 }
            );
            ctx.DisplayIds.Add(new DisplayIdRow { Kind = "definition", DisplayId = "DISP-B", ResourceId = def2, CreatedAt = t2 });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService();
        var compiler = new StubCompiler(compiledJson: "{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        // Assert
        var res = await sut.ListAsync("t1", CancellationToken.None);
        Assert.Equal(2, res.Count);

        // CreatedAt 順（定義リポジトリが CreatedAt 昇順）
        Assert.Equal(def1.ToString(), res[0].DisplayId);
        Assert.Equal("DISP-B", res[1].DisplayId);
    }

    /// <summary>
    /// ページ取得で一致件数とHasMoreが総件数に応じて設定されることを確認する。
    /// </summary>
    [Fact]
    public async Task ListPagedAsync_HasMoreReflectsTotal()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        // Act
        var def1 = Guid.NewGuid();
        var def2 = Guid.NewGuid();
        var def3 = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(inDb.Options))
        {
            ctx.WorkflowDefinitions.AddRange(
                new WorkflowDefinitionRow { DefinitionId = def1, TenantId = "t1", Name = "order-1", SourceYaml = "x", CompiledJson = "{}", CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new WorkflowDefinitionRow { DefinitionId = def2, TenantId = "t1", Name = "order-2", SourceYaml = "x", CompiledJson = "{}", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new WorkflowDefinitionRow { DefinitionId = def3, TenantId = "t1", Name = "payment", SourceYaml = "x", CompiledJson = "{}", CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService();
        var compiler = new StubCompiler(compiledJson: "{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        // Assert
        var page = await sut.ListPagedAsync("t1", offset: 0, limit: 1, nameContains: "order", CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.True(page.HasMore);
    }

    /// <summary>
    /// 識別子解決に失敗したとき未検出例外を投げる。
    /// </summary>
    [Fact]
    public async Task GetAsync_ThrowsNotFound_WhenResolveReturnsNull()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        var display = new StubDisplayIdService();
        var compiler = new StubCompiler("{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync("t1", Guid.NewGuid().ToString(), CancellationToken.None));
    }

    /// <summary>
    /// 解決後に定義行がないとき未検出例外を投げる。
    /// </summary>
    [Fact]
    public async Task GetAsync_ThrowsNotFound_WhenRowMissing()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        var guid = Guid.NewGuid();
        var display = new StubDisplayIdService();
        display.ResolveMap["definition|" + guid.ToString()] = guid;

        var compiler = new StubCompiler("{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync("t1", guid.ToString(), CancellationToken.None));
    }

    /// <summary>
    /// 表示用識別子が空のとき識別子文字列へフォールバックする。
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenGetDisplayIdReturnsNull_FallsBackToUuidString()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);

        // Act
        var guid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(inDb.Options))
        {
            ctx.WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = guid,
                TenantId = "t1",
                Name = "def",
                SourceYaml = "x",
                CompiledJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService();
        display.ResolveMap["definition|" + guid.ToString()] = guid;
        // GetDisplayIdAsync は未登録 => null

        var compiler = new StubCompiler("{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);

        // Assert
        var res = await sut.GetAsync("t1", guid.ToString(), CancellationToken.None);
        Assert.Equal(guid.ToString(), res.DisplayId);
        Assert.Equal("def", res.Name);
    }

    /// <summary>
    /// 名前未指定で作成要求したとき、422 用の検証例外を返す。
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenNameMissing_ThrowsApiValidationExceptionWithNameDetails()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);
        var display = new StubDisplayIdService();
        var compiler = new StubCompiler("{}");
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);
        var request = new CreateDefinitionRequest { Name = " ", Yaml = "workflow:\n  name: x" };

        // Act
        var ex = await Assert.ThrowsAsync<ApiValidationException>(() => sut.CreateAsync("t1", request, CancellationToken.None));

        // Assert
        Assert.Equal("Definition name is required.", ex.Message);
        Assert.NotNull(ex.Details);
    }

    /// <summary>
    /// コンパイル失敗時に、422 用の検証例外へラップして返す。
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenCompilerThrowsArgumentException_WrapsToApiValidationException()
    {
        // Arrange
        using var inDb = new InMemoryTestDatabase();
        var definitionsRepo = new DefinitionRepository(inDb.Factory);
        var display = new StubDisplayIdService();
        var compiler = new ThrowingCompiler(new ArgumentException("yaml parse failed"));
        var idGen = new FixedIdGenerator(Guid.NewGuid());
        var sut = new DefinitionService(display, compiler, definitionsRepo, idGen);
        var request = new CreateDefinitionRequest { Name = "def", Yaml = "invalid: [" };

        // Act
        var ex = await Assert.ThrowsAsync<ApiValidationException>(() => sut.CreateAsync("t1", request, CancellationToken.None));

        // Assert
        Assert.Equal("Definition validation failed.", ex.Message);
        Assert.NotNull(ex.Details);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }
}


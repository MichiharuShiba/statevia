using Statevia.Service.Api.Abstractions.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Core.Application.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>project_accesses 認可 truth の結合テスト（ユースケース層）。</summary>
public sealed class ProjectAuthorizationIntegrationTests
{
    /// <summary>別テナントは project_access 未付与で定義 GET できない。</summary>
    [Fact]
    public async Task GetAsync_CrossTenantWithoutAccess_ThrowsNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var ownerTenantId = TestTenantIds.DefaultTenantId;
        var defId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var seed = new CoreDbContext(db.Options))
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "default",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            var project = ProjectTestData.AddDefaultProject(seed, ownerTenantId, "default");
            DefinitionTestData.AddDefinitionWithVersion(seed, ownerTenantId, defId, "shared-def", project.ProjectId);
            await seed.SaveChangesAsync();
        }

        var display = new ResolvingDisplayIdService(defId);
        var executor = new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory));
        var projectRepo = new ProjectRepository(new DefaultIdGenerator());
        var sut = new DefinitionService(
            display,
            new NoopCompiler(),
            TestRepositoryFactory.CreateDefinitionRepository(),
            projectRepo,
            new ProjectAuthorizationService(projectRepo),
            new FixedTenantContextAccessor(new TenantContextState(
                TestTenantIds.OtherTenantId,
                "other",
                null,
                TenantLifecycle.Active)),
            new AllowAllRuntimePermissionAuthorization(),
            new DefaultIdGenerator(),
            executor);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync(defId.ToString(), CancellationToken.None));
    }

    private sealed class ResolvingDisplayIdService : IDisplayIdService, IDisplayIdWriteService
    {
        private readonly Guid _id;

        public ResolvingDisplayIdService(Guid id) => _id = id;

        public Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(_id);

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(
            string kind,
            IEnumerable<Guid> resourceIds,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoopCompiler : IDefinitionCompilerService
    {
        public (Statevia.Core.Engine.Abstractions.CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
            string name,
            string yaml,
            Guid? tenantId = null) =>
            throw new NotSupportedException();

        public Statevia.Core.Engine.Abstractions.CompiledWorkflowDefinition RestoreFromStoredVersion(
            string sourceYaml,
            string compiledJson) =>
            throw new NotSupportedException();
    }
}

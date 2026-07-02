using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>IDefinitionRepository のテスト用スタブ。</summary>
internal sealed class StubDefinitionRepository : IDefinitionRepository
{
    public DefinitionDetail? LatestDetail { get; init; }

    public DefinitionVersionRow? VersionById { get; init; }

    public DefinitionVersionRow? VersionByNumber { get; init; }

    public Exception? AddWithInitialVersionException { get; set; }

    public Task<DefinitionDetail?> GetLatestByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = ct;
        if (LatestDetail is null || LatestDetail.Definition.DefinitionId != definitionId)
            return Task.FromResult<DefinitionDetail?>(null);

        return Task.FromResult<DefinitionDetail?>(LatestDetail);
    }

    public Task<DefinitionVersionRow?> GetVersionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionVersionId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = ct;
        if (VersionById is null || VersionById.DefinitionVersionId != definitionVersionId)
            return Task.FromResult<DefinitionVersionRow?>(null);

        return LatestDetail is { } detail && VersionById.DefinitionId == detail.Definition.DefinitionId
            ? Task.FromResult<DefinitionVersionRow?>(VersionById)
            : Task.FromResult<DefinitionVersionRow?>(null);
    }

    public Task<DefinitionVersionRow?> GetVersionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = ct;
        if (VersionByNumber is null
            || VersionByNumber.DefinitionId != definitionId
            || VersionByNumber.Version != version)
            return Task.FromResult<DefinitionVersionRow?>(null);

        return LatestDetail is not null
            ? Task.FromResult<DefinitionVersionRow?>(VersionByNumber)
            : Task.FromResult<DefinitionVersionRow?>(null);
    }

    public Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct)
    {
        _ = uow;
        _ = definition;
        _ = version;
        _ = ct;
        if (AddWithInitialVersionException is not null)
            throw AddWithInitialVersionException;

        return Task.CompletedTask;
    }

    public Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct) =>
        Task.FromResult<DefinitionDetail?>(null);

    public Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = query;
        _ = ct;
        return Task.FromResult((0, new List<(DefinitionDetail, string?)>()));
    }

    public Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = ct;
        if (LatestDetail is null || LatestDetail.Definition.DefinitionId != definitionId)
            return Task.FromResult<Guid?>(null);

        return Task.FromResult<Guid?>(LatestDetail.Definition.ProjectId);
    }
}

/// <summary>definitionId に紐づく最新版を返すスタブを生成する。</summary>
internal static class StubDefinitionRepositoryFactory
{
    public static StubDefinitionRepository ForDefinition(
        Guid definitionId,
        Guid tenantId,
        string name,
        Guid? projectId = null,
        string sourceYaml = "yaml",
        string compiledJson = "{}")
    {
        var now = DateTime.UtcNow;
        var definition = new DefinitionRow
        {
            DefinitionId = definitionId,
            TenantId = tenantId,
            ProjectId = projectId ?? Guid.NewGuid(),
            Slug = DefinitionSlug.FromName(definitionId, name),
            Name = name,
            LatestVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var version = new DefinitionVersionRow
        {
            DefinitionVersionId = Guid.NewGuid(),
            DefinitionId = definitionId,
            Version = 1,
            SourceYaml = sourceYaml,
            CompiledJson = compiledJson,
            CreatedAt = now
        };
        return new StubDefinitionRepository
        {
            LatestDetail = new DefinitionDetail { Definition = definition, Version = version }
        };
    }
}

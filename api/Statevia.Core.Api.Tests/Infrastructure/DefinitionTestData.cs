using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary>definitions / definition_versions のテストデータ投入。</summary>
internal static class DefinitionTestData
{
    /// <summary>定義と初版を DB に追加する。</summary>
    public static (DefinitionRow Definition, DefinitionVersionRow Version) AddDefinitionWithVersion(
        CoreDbContext ctx,
        string tenantId,
        Guid definitionId,
        string name,
        string sourceYaml = "x",
        string compiledJson = "{}",
        int version = 1,
        Guid? versionId = null,
        DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        var definition = new DefinitionRow
        {
            DefinitionId = definitionId,
            TenantId = tenantId,
            Slug = DefinitionSlug.FromName(definitionId, name),
            Name = name,
            LatestVersion = version,
            CreatedAt = now,
            UpdatedAt = now
        };
        var versionRow = new DefinitionVersionRow
        {
            DefinitionVersionId = versionId ?? Guid.NewGuid(),
            DefinitionId = definitionId,
            Version = version,
            SourceYaml = sourceYaml,
            CompiledJson = compiledJson,
            CreatedAt = now
        };
        ctx.Definitions.Add(definition);
        ctx.DefinitionVersions.Add(versionRow);
        return (definition, versionRow);
    }
}

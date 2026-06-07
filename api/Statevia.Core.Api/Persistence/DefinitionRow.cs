namespace Statevia.Core.Api.Persistence;

/// <summary>definitions テーブル（論理定義の投影メタ）。</summary>
internal class DefinitionRow
{
    public Guid DefinitionId { get; set; }

    /// <summary>所属テナント（<c>tenants.tenant_id</c> FK）。</summary>
    public Guid TenantId { get; set; }

    /// <summary>所属 project（NOT NULL。認可は project_accesses が truth）。</summary>
    public Guid ProjectId { get; set; }

    /// <summary>project 内 slug（移行期は tenant 内で一意）。</summary>
    public required string Slug { get; set; }

    /// <summary>表示名（API の name）。</summary>
    public required string Name { get; set; }

    /// <summary>最新版番号（投影。truth は definition_versions）。</summary>
    public int LatestVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

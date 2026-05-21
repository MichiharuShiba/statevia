namespace Statevia.Core.Api.Persistence;

/// <summary>definitions テーブル（論理定義の投影メタ）。</summary>
internal class DefinitionRow
{
    public Guid DefinitionId { get; set; }

    /// <summary>移行期（project 未導入）のテナント境界。1b で project 経由に移行予定。</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>所属 project（フェーズ 1b まで NULL 可）。</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>project 内 slug（移行期は tenant 内で一意）。</summary>
    public required string Slug { get; set; }

    /// <summary>表示名（API の name）。</summary>
    public required string Name { get; set; }

    /// <summary>最新版番号（投影。truth は definition_versions）。</summary>
    public int LatestVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

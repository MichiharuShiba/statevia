using Statevia.Service.Api.Application.Security;

namespace Statevia.Service.Api.Persistence;

/// <summary>projects テーブル。</summary>
internal sealed class ProjectRow
{
    public Guid ProjectId { get; set; }

    /// <summary>オーナーテナント内部 UUID（tenants.tenant_id）。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>オーナーテナント内 slug。</summary>
    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>discoverability ヒント（認可 truth ではない）。</summary>
    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>project_accesses テーブル — 認可 truth。</summary>
internal sealed class ProjectAccessRow
{
    public Guid ProjectId { get; set; }

    /// <summary>付与先テナント内部 UUID。</summary>
    public Guid TenantId { get; set; }

    public ProjectAccessRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}

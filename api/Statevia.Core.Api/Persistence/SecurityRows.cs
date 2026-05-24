using Statevia.Core.Api.Application.Security;



namespace Statevia.Core.Api.Persistence;



/// <summary><c>tenants</c> 行。</summary>

internal sealed class TenantRow

{

    public Guid TenantId { get; set; }

    public string TenantKey { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public TenantLifecycle Lifecycle { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

}



/// <summary><c>permission_definitions</c> 行。</summary>

internal sealed class PermissionDefinitionRow

{

    public Guid PermissionDefinitionId { get; set; }

    public string PermissionKey { get; set; } = "";

    public string DisplayLabel { get; set; } = "";

    public string? DisplayKey { get; set; }

    public string? OwnerType { get; set; }

    public string? OwnerKey { get; set; }

    public bool IsSystem { get; set; }

    public bool IsDeprecated { get; set; }

    public DateTime CreatedAt { get; set; }

}



/// <summary><c>principals</c> 行。</summary>

internal sealed class PrincipalRow

{

    public Guid PrincipalId { get; set; }

    public Guid TenantId { get; set; }

    public PrincipalScope PrincipalScope { get; set; }

    public PrincipalType PrincipalType { get; set; }

    public string DisplayName { get; set; } = "";

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DisabledAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

}



/// <summary><c>user_principals</c> 行。</summary>

internal sealed class UserPrincipalRow

{

    public Guid PrincipalId { get; set; }

    public Guid UserId { get; set; }

}



/// <summary><c>users</c> 行。</summary>

internal sealed class UserRow

{

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public bool IsTenantAdmin { get; set; }

    public bool IsPlatformAdmin { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DisabledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

}



/// <summary><c>groups</c> 行。</summary>

internal sealed class GroupRow

{

    public Guid GroupId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

}



/// <summary><c>group_permissions</c> 行。</summary>

internal sealed class GroupPermissionRow

{

    public Guid GroupId { get; set; }

    public string PermissionKey { get; set; } = "";

}



/// <summary><c>user_group_members</c> 行。</summary>

internal sealed class UserGroupMemberRow

{

    public Guid UserId { get; set; }

    public Guid GroupId { get; set; }

}



/// <summary><c>service_accounts</c> 行。</summary>

internal sealed class ServiceAccountRow

{

    public Guid ServiceAccountId { get; set; }

    public Guid TenantId { get; set; }

    public Guid PrincipalId { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

}



/// <summary><c>api_keys</c> 行。</summary>

internal sealed class ApiKeyRow

{

    public Guid ApiKeyId { get; set; }

    public Guid TenantId { get; set; }

    public Guid PrincipalId { get; set; }

    public string KeyPrefix { get; set; } = "";

    public string KeyHash { get; set; } = "";

    public string AllowedScopesJson { get; set; } = "[]";

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

}



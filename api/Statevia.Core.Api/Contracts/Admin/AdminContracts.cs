using System.ComponentModel.DataAnnotations;

namespace Statevia.Core.Api.Contracts.Admin;

/// <summary>権限カタログ 1 件。</summary>
public sealed class PermissionDefinitionDto
{
    /// <summary>semantic key。</summary>
    public string PermissionKey { get; set; } = "";

    /// <summary>表示ラベル。</summary>
    public string DisplayLabel { get; set; } = "";

    /// <summary>i18n 辞書キー（任意）。</summary>
    public string? DisplayKey { get; set; }

    /// <summary>システム予約か。</summary>
    public bool IsSystem { get; set; }

    /// <summary>非推奨か。</summary>
    public bool IsDeprecated { get; set; }
}

/// <summary>テナントユーザー一覧項目。</summary>
public sealed class AdminUserListItemDto
{
    /// <summary>ユーザー ID。</summary>
    public Guid UserId { get; set; }

    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>メールアドレス。</summary>
    public string Email { get; set; } = "";

    /// <summary>Principal 表示名。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>テナント管理者か。</summary>
    public bool IsTenantAdmin { get; set; }

    /// <summary>有効か。</summary>
    public bool IsActive { get; set; }

    /// <summary>所属グループ ID。</summary>
    public IReadOnlyList<Guid> GroupIds { get; set; } = Array.Empty<Guid>();

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>ユーザー作成要求。</summary>
public sealed class CreateAdminUserRequest : IValidatableObject
{
    /// <summary>メールアドレス。</summary>
    [Required]
    public string Email { get; set; } = "";

    /// <summary>平文パスワード。</summary>
    [Required]
    public string Password { get; set; } = "";

    /// <summary>Principal 表示名（未指定時は email）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>テナント管理者にするか（未指定時は false）。</summary>
    public bool? IsTenantAdmin { get; set; }

    /// <summary>初期所属グループ ID（任意）。</summary>
    public IReadOnlyList<Guid>? GroupIds { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Email))
            yield return new ValidationResult("email is required.", [nameof(Email)]);
        if (string.IsNullOrWhiteSpace(Password))
            yield return new ValidationResult("password is required.", [nameof(Password)]);
    }
}

/// <summary>ユーザー更新要求。</summary>
public sealed class UpdateAdminUserRequest
{
    /// <summary>有効か。false で無効化。</summary>
    public bool? IsActive { get; set; }

    /// <summary>テナント管理者フラグ。</summary>
    public bool? IsTenantAdmin { get; set; }
}

/// <summary>グループ一覧項目。</summary>
public sealed class AdminGroupListItemDto
{
    /// <summary>グループ ID。</summary>
    public Guid GroupId { get; set; }

    /// <summary>グループ名。</summary>
    public string Name { get; set; } = "";

    /// <summary>システム予約か。</summary>
    public bool IsSystem { get; set; }

    /// <summary>メンバー数。</summary>
    public int MemberCount { get; set; }

    /// <summary>付与権限数。</summary>
    public int PermissionCount { get; set; }

    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>グループ詳細。</summary>
public sealed class AdminGroupDetailDto
{
    /// <summary>グループ ID。</summary>
    public Guid GroupId { get; set; }

    /// <summary>グループ名。</summary>
    public string Name { get; set; } = "";

    /// <summary>システム予約か。</summary>
    public bool IsSystem { get; set; }

    /// <summary>メンバー Principal ID。</summary>
    public IReadOnlyList<Guid> MemberUserIds { get; set; } = Array.Empty<Guid>();

    /// <summary>付与 semantic key。</summary>
    public IReadOnlyList<string> PermissionKeys { get; set; } = Array.Empty<string>();
}

/// <summary>グループ作成要求。</summary>
public sealed class CreateAdminGroupRequest : IValidatableObject
{
    private const int MaxNameLength = 128;

    /// <summary>グループ名。</summary>
    [Required]
    public string Name { get; set; } = "";

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedName = Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            yield return new ValidationResult("name is required.", [nameof(Name)]);
        else if (trimmedName.Length > MaxNameLength)
            yield return new ValidationResult("name must be at most 128 characters.", [nameof(Name)]);
    }
}

/// <summary>グループメンバー更新要求。</summary>
public sealed class SetAdminGroupMembersRequest
{
    /// <summary>所属させるユーザー ID。</summary>
    public IReadOnlyList<Guid> UserIds { get; set; } = Array.Empty<Guid>();
}

/// <summary>グループ権限更新要求。</summary>
public sealed class SetAdminGroupPermissionsRequest
{
    /// <summary>付与する semantic key（<c>tenant.admin</c> は不可）。</summary>
    public IReadOnlyList<string> PermissionKeys { get; set; } = Array.Empty<string>();
}

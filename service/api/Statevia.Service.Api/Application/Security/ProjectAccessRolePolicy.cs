namespace Statevia.Service.Api.Application.Security;

/// <summary><see cref="ProjectAccessRole"/> の比較と文字列変換。</summary>
public static class ProjectAccessRolePolicy
{
    /// <summary>実効ロールが要求最小ロールを満たすか。</summary>
    /// <param name="effectiveRole">実効ロール。</param>
    /// <param name="minimumRole">要求最小ロール。</param>
    public static bool MeetsMinimum(ProjectAccessRole effectiveRole, ProjectAccessRole minimumRole) =>
        effectiveRole >= minimumRole;

    /// <summary>DB 文字列を <see cref="ProjectAccessRole"/> に変換する。</summary>
    /// <param name="value">永続化された role 文字列。</param>
    /// <param name="role">変換結果。失敗時は <see langword="null"/>。</param>
    public static bool TryParse(string? value, out ProjectAccessRole? role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = null;
            return false;
        }

        if (Enum.TryParse(value, ignoreCase: true, out ProjectAccessRole parsed))
        {
            role = parsed;
            return true;
        }

        role = null;
        return false;
    }

    /// <summary>永続化用の role 文字列を返す。</summary>
    /// <param name="role">ロール。</param>
    public static string ToStorageValue(ProjectAccessRole role) =>
        role switch
        {
            ProjectAccessRole.Reader => "reader",
            ProjectAccessRole.Executor => "executor",
            ProjectAccessRole.Publisher => "publisher",
            ProjectAccessRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown project access role.")
        };

    /// <summary>最小ロール以上のロール一覧（クエリ用）。</summary>
    /// <param name="minimumRole">要求最小ロール。</param>
    public static IReadOnlyList<ProjectAccessRole> RolesAtOrAbove(ProjectAccessRole minimumRole) =>
        Enum.GetValues<ProjectAccessRole>()
            .Where(role => role >= minimumRole)
            .ToArray();
}

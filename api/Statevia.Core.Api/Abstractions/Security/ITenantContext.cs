namespace Statevia.Core.Api.Abstractions.Security;

/// <summary>現在リクエスト／ワーカーのテナント文脈（読み取り専用）。</summary>
public interface ITenantContext
{
    /// <summary>テナントが解決済みか。</summary>
    bool IsResolved { get; }

    /// <summary>テナント内部 UUID。未解決時は <see langword="null"/>。</summary>
    Guid? TenantInternalId { get; }

    /// <summary>外部向け <c>tenant_key</c>。未解決時は <see langword="null"/>。</summary>
    string? TenantKey { get; }

    /// <summary>Principal UUID。認証済みの場合のみ。</summary>
    Guid? PrincipalId { get; }

    /// <summary>
    /// API キー認証時の交差済み permission key。JWT 等では <see langword="null"/>（都度展開）。
    /// </summary>
    IReadOnlySet<string>? EffectivePermissionKeys { get; }
}

/// <summary>
/// テナント文脈の設定・参照。HTTP ミドルウェアと Worker スコープが利用する。
/// </summary>
public interface ITenantContextAccessor : ITenantContext
{
    /// <summary>現在の文脈を上書きする（スコープ終了時に復元すること）。</summary>
    /// <param name="state">新しい文脈。<see langword="null"/> でクリア。</param>
    /// <returns>復元用の disposable。</returns>
    IDisposable SetContext(TenantContextState? state);
}

/// <summary>テナント文脈のスナップショット。</summary>
/// <param name="TenantInternalId">内部 UUID。</param>
/// <param name="TenantKey">外部キー。</param>
/// <param name="PrincipalId">Principal（任意）。</param>
/// <param name="Lifecycle">テナントライフサイクル。</param>
/// <param name="EffectivePermissionKeys">API キー認証時の交差済み permission（任意）。</param>
public sealed record TenantContextState(
    Guid TenantInternalId,
    string TenantKey,
    Guid? PrincipalId,
    Application.Security.TenantLifecycle Lifecycle,
    IReadOnlySet<string>? EffectivePermissionKeys = null);

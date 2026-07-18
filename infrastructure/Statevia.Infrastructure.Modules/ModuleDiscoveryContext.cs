namespace Statevia.Infrastructure.Modules;

/// <summary>
/// Module discover のスコープを呼び出し単位で渡す。
/// </summary>
/// <remarks>
/// <see cref="ModuleHost.LoadAsync"/> が discover 直前に設定し、完了後にクリアする。
/// テナント単位 load では remote Source をスキップし、OwnerTenantId の上書きを防ぐ。
/// </remarks>
internal static class ModuleDiscoveryContext
{
    private static readonly AsyncLocal<string?> TenantKeyLocal = new();
    private static readonly AsyncLocal<bool?> DiscoverFilesystemLocal = new();
    private static readonly AsyncLocal<bool?> DiscoverRemoteLocal = new();

    /// <summary>
    /// 現在の discover 対象 <c>tenant_key</c>（filesystem discover 時は必須）。
    /// </summary>
    public static string? TenantKey
    {
        get => TenantKeyLocal.Value;
        set => TenantKeyLocal.Value = value;
    }

    /// <summary>filesystem Source を discover するか。未設定時は <see langword="true"/>。</summary>
    public static bool DiscoverFilesystem
    {
        get => DiscoverFilesystemLocal.Value ?? true;
        set => DiscoverFilesystemLocal.Value = value;
    }

    /// <summary>リモート Source（OCI / S3 / Git）を discover するか。未設定時は <see langword="true"/>。</summary>
    public static bool DiscoverRemote
    {
        get => DiscoverRemoteLocal.Value ?? true;
        set => DiscoverRemoteLocal.Value = value;
    }

    /// <summary>コンテキストをクリアする（既定の全 Source discover に戻す）。</summary>
    public static void Clear()
    {
        TenantKeyLocal.Value = null;
        DiscoverFilesystemLocal.Value = null;
        DiscoverRemoteLocal.Value = null;
    }
}

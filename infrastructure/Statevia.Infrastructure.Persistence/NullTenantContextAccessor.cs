namespace Statevia.Infrastructure.Persistence;

/// <summary>テナント文脈なし（fail-closed）のアクセサ。テストの既定値。</summary>
internal sealed class NullTenantContextAccessor : ITenantContextAccessor
{
    /// <summary>共有インスタンス。</summary>
    public static readonly NullTenantContextAccessor Instance = new();

    private NullTenantContextAccessor() { }

    /// <inheritdoc />
    public bool IsResolved => false;

    /// <inheritdoc />
    public Guid? TenantId => null;

    /// <inheritdoc />
    public string? TenantKey => null;

    /// <inheritdoc />
    public Guid? PrincipalId => null;

    /// <inheritdoc />
    public IReadOnlySet<string>? EffectivePermissionKeys => null;

    /// <inheritdoc />
    public IDisposable SetContext(TenantContextState? state) => NullScope.Empty;

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Empty = new();
        public void Dispose() { }
    }
}

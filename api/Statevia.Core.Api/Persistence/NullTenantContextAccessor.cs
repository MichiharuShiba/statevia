using Statevia.Core.Api.Abstractions.Security;

namespace Statevia.Core.Api.Persistence;

/// <summary>テナント文脈なし（fail-closed）のアクセサ。テストの既定値。</summary>
internal sealed class NullTenantContextAccessor : ITenantContextAccessor
{
    /// <summary>共有インスタンス。</summary>
    public static readonly NullTenantContextAccessor Instance = new();

    private NullTenantContextAccessor() { }

    /// <inheritdoc />
    public bool IsResolved => false;

    /// <inheritdoc />
    public Guid? TenantInternalId => null;

    /// <inheritdoc />
    public string? TenantKey => null;

    /// <inheritdoc />
    public Guid? PrincipalId => null;

    /// <inheritdoc />
    public IDisposable SetContext(TenantContextState? state) => NullScope.Empty;

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Empty = new();
        public void Dispose() { }
    }
}

using Statevia.Service.Api.Application.Security;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>テスト用にテナント文脈を明示設定できるアクセサ。</summary>
internal sealed class SettableTenantContextAccessor : ITenantContextAccessor
{
    private TenantContextState? _state;

    /// <inheritdoc />
    public bool IsResolved => _state is not null;

    /// <inheritdoc />
    public Guid? TenantId => _state?.TenantId;

    /// <inheritdoc />
    public string? TenantKey => _state?.TenantKey;

    /// <inheritdoc />
    public Guid? PrincipalId => _state?.PrincipalId;

    /// <inheritdoc />
    public IReadOnlySet<string>? EffectivePermissionKeys => _state?.EffectivePermissionKeys;

    /// <summary>文脈を直接設定する。</summary>
    /// <param name="state">適用する文脈。</param>
    public void Set(TenantContextState? state) => _state = state;

    /// <inheritdoc />
    public IDisposable SetContext(TenantContextState? state)
    {
        var previous = _state;
        _state = state;
        return new Scope(this, previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly SettableTenantContextAccessor _accessor;
        private readonly TenantContextState? _previous;
        private bool _disposed;

        public Scope(SettableTenantContextAccessor accessor, TenantContextState? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _accessor._state = _previous;
        }
    }
}

/// <summary>テスト用の既定テナント ID。</summary>
internal static class TestTenantIds
{
    /// <summary><c>tenant_key = default</c> の内部 UUID。</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-4000-8000-000000000001");

    /// <summary><c>tenant_key = t1</c> の内部 UUID（実行系テスト用）。</summary>
    public static readonly Guid T1TenantId = Guid.Parse("00000000-0000-4000-8000-000000000002");

    /// <summary><c>tenant_key = other</c> の内部 UUID（テナント分離テスト用）。</summary>
    public static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-4000-8000-000000000003");

    /// <summary>既定テナント文脈。</summary>
    public static TenantContextState DefaultContext =>
        new(DefaultTenantId, "default", null, TenantLifecycle.Active);

    /// <summary><c>tenant_key = tenant-a</c> の内部 UUID（dedup リポジトリテスト用）。</summary>
    public static readonly Guid TenantAId = Guid.Parse("00000000-0000-4000-8000-000000000004");

    /// <summary><c>t1</c> テナント文脈。</summary>
    public static TenantContextState T1Context =>
        new(T1TenantId, "t1", null, TenantLifecycle.Active);
}

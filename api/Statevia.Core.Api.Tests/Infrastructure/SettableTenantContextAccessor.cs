using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Security;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary>テスト用にテナント文脈を明示設定できるアクセサ。</summary>
internal sealed class SettableTenantContextAccessor : ITenantContextAccessor
{
    private TenantContextState? _state;

    /// <inheritdoc />
    public bool IsResolved => _state is not null;

    /// <inheritdoc />
    public Guid? TenantInternalId => _state?.TenantInternalId;

    /// <inheritdoc />
    public string? TenantKey => _state?.TenantKey;

    /// <inheritdoc />
    public Guid? PrincipalId => _state?.PrincipalId;

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
    public static readonly Guid DefaultInternalId = Guid.Parse("00000000-0000-4000-8000-000000000001");

    /// <summary>既定テナント文脈。</summary>
    public static TenantContextState DefaultContext =>
        new(DefaultInternalId, "default", null, TenantLifecycle.Active);
}

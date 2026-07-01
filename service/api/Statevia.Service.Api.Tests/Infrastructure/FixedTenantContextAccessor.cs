using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Application.Security;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>テスト用 ITenantContextAccessor スタブ。</summary>
internal sealed class FixedTenantContextAccessor : ITenantContextAccessor
{
    private readonly TenantContextState? _state;

    /// <summary>固定文脈で初期化する。</summary>
    public FixedTenantContextAccessor(TenantContextState? state) => _state = state;

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

    /// <inheritdoc />
    public IDisposable SetContext(TenantContextState? state) => new NoopScope();

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

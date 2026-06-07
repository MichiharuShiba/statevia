using Statevia.Core.Api.Abstractions.Security;

namespace Statevia.Core.Api.Infrastructure.Security;

/// <summary>AsyncLocal ベースのテナント文脈。</summary>
internal sealed class TenantContextAccessor : ITenantContextAccessor
{
    private readonly AsyncLocal<TenantContextState?> _current = new();

    /// <inheritdoc />
    public bool IsResolved => _current.Value is not null;

    /// <inheritdoc />
    public Guid? TenantInternalId => _current.Value?.TenantInternalId;

    /// <inheritdoc />
    public string? TenantKey => _current.Value?.TenantKey;

    /// <inheritdoc />
    public Guid? PrincipalId => _current.Value?.PrincipalId;

    /// <inheritdoc />
    public IReadOnlySet<string>? EffectivePermissionKeys => _current.Value?.EffectivePermissionKeys;

    /// <inheritdoc />
    public IDisposable SetContext(TenantContextState? state) => new Scope(this, _current.Value, state);

    private sealed class Scope : IDisposable
    {
        private readonly TenantContextAccessor _accessor;
        private readonly TenantContextState? _previous;
        private bool _disposed;

        public Scope(TenantContextAccessor accessor, TenantContextState? previous, TenantContextState? next)
        {
            _accessor = accessor;
            _previous = previous;
            _accessor._current.Value = next;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _accessor._current.Value = _previous;
        }
    }
}

/// <summary>
/// ワーカー等でテナント文脈を張る標準入口。AsyncLocal 単独依存を避け、明示スコープで設定する。
/// </summary>
internal static class TenantExecutionScope
{
    /// <summary>指定テナント文脈で <paramref name="action"/> を実行する。</summary>
    /// <param name="accessor">テナント文脈アクセサ。</param>
    /// <param name="state">適用する文脈。</param>
    /// <param name="action">実行する処理。</param>
    public static async Task RunAsync(
        ITenantContextAccessor accessor,
        TenantContextState state,
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(action);

        using (accessor.SetContext(state))
            await action().ConfigureAwait(false);
    }

    /// <summary>指定テナント文脈で <paramref name="action"/> を実行し結果を返す。</summary>
    /// <typeparam name="T">戻り値の型。</typeparam>
    /// <param name="accessor">テナント文脈アクセサ。</param>
    /// <param name="state">適用する文脈。</param>
    /// <param name="action">実行する処理。</param>
    /// <returns><paramref name="action"/> の結果。</returns>
    public static async Task<T> RunAsync<T>(
        ITenantContextAccessor accessor,
        TenantContextState state,
        Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(action);

        using (accessor.SetContext(state))
            return await action().ConfigureAwait(false);
    }
}

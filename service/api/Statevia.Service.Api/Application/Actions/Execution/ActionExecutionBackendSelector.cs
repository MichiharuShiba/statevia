using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>DI 登録された <see cref="IActionExecutionBackend"/> 群から Mode 単位で実装を選択する。</summary>
/// <remarks>
/// <para>選択規則: ある Mode に単一登録なら自動選択。複数登録時は
/// <c>Statevia:ExecutionPolicy:Backends:{Mode}</c> の ProviderKey で明示指定する。</para>
/// <para>未指定かつ複数登録、または Mode 未登録は安全側で解決失敗（fail-safe）とし、
/// 呼び出し側が <c>UnsupportedExecutionMode</c> を返す。</para>
/// </remarks>
internal sealed class ActionExecutionBackendSelector : IActionExecutionBackendSelector
{
    private readonly Dictionary<ActionExecutionMode, IReadOnlyList<IActionExecutionBackend>> _backendsByMode;
    private readonly ExecutionPolicyOptions _options;

    /// <summary>セレクタを構築する。</summary>
    /// <param name="backends">DI 登録された Backend 群。</param>
    /// <param name="options">Backend 選択設定を含む実行ポリシー設定。</param>
    public ActionExecutionBackendSelector(
        IEnumerable<IActionExecutionBackend> backends,
        IOptions<ExecutionPolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(options);

        _backendsByMode = backends
            .GroupBy(backend => backend.Mode)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<IActionExecutionBackend>)group.ToList());
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool TryResolve(
        ActionExecutionMode mode,
        ActionExecutionContext context,
        [NotNullWhen(true)] out IActionExecutionBackend? backend)
    {
        backend = null;

        if (!_backendsByMode.TryGetValue(mode, out var candidates) || candidates.Count == 0)
        {
            return false;
        }

        if (candidates.Count == 1)
        {
            backend = candidates[0];
            return true;
        }

        // 複数登録時は ProviderKey の明示指定を必須とする（未指定は fail-safe）。
        if (_options.Backends.TryGetValue(mode.ToString(), out var providerKey)
            && !string.IsNullOrWhiteSpace(providerKey))
        {
            backend = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderKey, providerKey, StringComparison.Ordinal));
        }

        return backend is not null;
    }
}

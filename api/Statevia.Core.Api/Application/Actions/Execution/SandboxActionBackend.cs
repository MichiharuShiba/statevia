using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>サンドボックス隔離（Container / WASM）系 Backend の共通基底。</summary>
/// <remarks>
/// <para>設定された ProviderKey に対応する <see cref="IActionSandboxRuntime"/> を解決して実行を委譲する。</para>
/// <para>ランタイム未構成・未登録時は <c>SandboxRuntimeNotConfigured</c> で安全側に失敗する（fail-safe）。</para>
/// <para>Engine 内部状態（<c>StateContext</c>）は委譲せず、リクエストと入力のみを渡す。</para>
/// </remarks>
/// <param name="runtimes">DI 登録されたサンドボックスランタイム群。</param>
/// <param name="options">サンドボックス設定を含む実行ポリシー設定。</param>
internal abstract class SandboxActionBackend(
    IEnumerable<IActionSandboxRuntime> runtimes,
    IOptions<ExecutionPolicyOptions> options) : IActionExecutionBackend
{
    private readonly Dictionary<string, IActionSandboxRuntime> _runtimesByProviderKey = runtimes
        .GroupBy(runtime => runtime.ProviderKey, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private readonly ExecutionPolicyOptions _options = options.Value;

    /// <inheritdoc />
    public abstract ActionExecutionMode Mode { get; }

    /// <inheritdoc />
    public abstract string ProviderKey { get; }

    /// <summary>この Mode が使用するランタイムの ProviderKey を設定から解決する。</summary>
    /// <param name="sandbox">サンドボックス設定。</param>
    protected abstract string? ResolveRuntimeProviderKey(SandboxOptions sandbox);

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionBackendInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var sandbox = _options.Sandbox;
        var runtimeProviderKey = ResolveRuntimeProviderKey(sandbox);

        if (string.IsNullOrWhiteSpace(runtimeProviderKey)
            || !_runtimesByProviderKey.TryGetValue(runtimeProviderKey, out var runtime))
        {
            return Task.FromResult(new ActionExecutionResult
            {
                Success = false,
                ErrorCode = "SandboxRuntimeNotConfigured",
                ErrorMessage = $"No sandbox runtime is configured for execution mode '{Mode}'.",
            });
        }

        var request = ActionExecutionRuntimeInputMapper.WithRuntimeInput(
            invocation.Request,
            invocation.RuntimeInput);
        return runtime.RunAsync(request, sandbox.ToLimits(), cancellationToken);
    }
}

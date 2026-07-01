using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>WASM サンドボックス実行 Backend（Phase 4 スタブ。具体ランタイムは後続）。</summary>
/// <param name="runtimes">DI 登録されたサンドボックスランタイム群。</param>
/// <param name="options">サンドボックス設定を含む実行ポリシー設定。</param>
internal sealed class WasmActionBackend(
    IEnumerable<IActionSandboxRuntime> runtimes,
    IOptions<ExecutionPolicyOptions> options) : SandboxActionBackend(runtimes, options)
{
    /// <inheritdoc />
    public override ActionExecutionMode Mode => ActionExecutionMode.Wasm;

    /// <inheritdoc />
    public override string ProviderKey => "wasm";

    /// <inheritdoc />
    protected override string? ResolveRuntimeProviderKey(SandboxOptions sandbox) => sandbox.WasmProvider;
}

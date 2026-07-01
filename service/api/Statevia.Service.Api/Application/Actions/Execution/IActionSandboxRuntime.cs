using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>隔離ランタイムの差し替え点（Docker / Podman / K8s / Firecracker / Wasmtime 等）。</summary>
/// <remarks>
/// <para>Container / WASM Backend が選択して実行を委譲する。CPU / メモリ / ファイルシステム / ネットワークの
/// 隔離責務は本インターフェースの実装側が負う。</para>
/// <para>セキュリティ境界: Engine 内部状態（<c>StateContext.Events</c> / <c>Store</c>）は渡さない。
/// 実行に必要な情報は <see cref="ActionExecutionRequest"/> と入力のみ（Action Host と同方針）。</para>
/// </remarks>
internal interface IActionSandboxRuntime
{
    /// <summary>同一隔離レベル内で実装を識別するキー（例: <c>docker</c> / <c>wasmtime</c>）。</summary>
    string ProviderKey { get; }

    /// <summary>リクエストをサンドボックス内で実行する。</summary>
    /// <param name="request">実行リクエスト（入力を含む）。</param>
    /// <param name="limits">課すリソース上限。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。</returns>
    Task<ActionExecutionResult> RunAsync(
        ActionExecutionRequest request,
        SandboxLimits limits,
        CancellationToken cancellationToken);
}

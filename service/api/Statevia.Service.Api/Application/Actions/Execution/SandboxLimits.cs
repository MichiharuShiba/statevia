namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>サンドボックス実行に課すリソース上限。</summary>
/// <remarks>
/// 実際の強制（cgroup / コンテナ制限など）は <see cref="IActionSandboxRuntime"/> 実装側の責務。
/// 値が <c>null</c> の項目は「未指定（ランタイム既定に委ねる）」を意味する。
/// </remarks>
/// <param name="CpuLimit">CPU 上限（コア数換算。例: 0.5）。</param>
/// <param name="MemoryLimitMiB">メモリ上限（MiB）。</param>
/// <param name="Timeout">実行タイムアウト。</param>
internal sealed record SandboxLimits(double? CpuLimit, int? MemoryLimitMiB, TimeSpan? Timeout);

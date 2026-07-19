namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>
/// サンドボックス実行（Container / WASM）の設定。
/// 設定セクション: <c>Statevia:ExecutionPolicy:Sandbox</c>。
/// </summary>
/// <remarks>
/// 各 Mode の <c>*Provider</c> に登録済み <see cref="IActionSandboxRuntime.ProviderKey"/> を指定する。
/// 未指定、または該当ランタイム未登録の場合、Backend は <c>SandboxRuntimeNotConfigured</c> で安全側に失敗する。
/// </remarks>
internal sealed class SandboxOptions
{
    /// <summary>
    /// <see cref="TimeoutSeconds"/> の下限（設定時）。
    /// <see cref="DockerSandboxOptions.MinDefaultTimeoutSeconds"/> と揃える。
    /// </summary>
    public const int MinTimeoutSeconds = DockerSandboxOptions.MinDefaultTimeoutSeconds;

    /// <summary>
    /// <see cref="TimeoutSeconds"/> の上限（設定時）。
    /// <see cref="DockerSandboxOptions.MaxDefaultTimeoutSeconds"/> と揃える。
    /// </summary>
    public const int MaxTimeoutSeconds = DockerSandboxOptions.MaxDefaultTimeoutSeconds;

    /// <summary>
    /// <see cref="MemoryLimitMiB"/> の下限（設定時、64 MiB）。
    /// Echo Action の実測安定床（32 MiB）に余裕を載せた運用下限。
    /// </summary>
    public const int MinMemoryLimitMiB = 64;

    /// <summary>
    /// <see cref="MemoryLimitMiB"/> の上限（設定時、8 GiB）。
    /// 共有ホストでの単一コンテナによるメモリ逼迫を抑える。
    /// </summary>
    public const int MaxMemoryLimitMiB = 8_192;

    /// <summary>
    /// <see cref="CpuLimit"/> の下限（設定時）。
    /// Echo Action は 0.1 でも成功するが、誤設定耐性のため 0.25 を運用下限とする。
    /// </summary>
    public const double MinCpuLimit = 0.25;

    /// <summary>
    /// <see cref="CpuLimit"/> の上限（設定時、コア数換算）。
    /// 共有ホストでの CPU 占有を抑える。
    /// </summary>
    public const double MaxCpuLimit = 8.0;

    /// <summary>Container 隔離で使用するランタイムの ProviderKey。</summary>
    public string? ContainerProvider { get; set; }

    /// <summary>WASM 隔離で使用するランタイムの ProviderKey。</summary>
    public string? WasmProvider { get; set; }

    /// <summary>CPU 上限（コア数換算）。未指定はランタイム既定。</summary>
    public double? CpuLimit { get; set; }

    /// <summary>メモリ上限（MiB）。未指定はランタイム既定。</summary>
    public int? MemoryLimitMiB { get; set; }

    /// <summary>実行タイムアウト（秒）。未指定はランタイム既定。</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Docker サンドボックス固有設定。
    /// セクション <c>Statevia:ExecutionPolicy:Sandbox:Docker</c>。
    /// </summary>
    public DockerSandboxOptions Docker { get; set; } = new();

    /// <summary>設定値から <see cref="SandboxLimits"/> を生成する。</summary>
    public SandboxLimits ToLimits() =>
        new(CpuLimit, MemoryLimitMiB, TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
}

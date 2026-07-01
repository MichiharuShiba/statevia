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

    /// <summary>設定値から <see cref="SandboxLimits"/> を生成する。</summary>
    public SandboxLimits ToLimits() =>
        new(CpuLimit, MemoryLimitMiB, TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
}

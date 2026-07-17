using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Docker 上の短命 Action Host コンテナで Action を実行する <see cref="IActionSandboxRuntime"/>。</summary>
/// <remarks>
/// <para>ProviderKey は <c>docker</c>。呼び出し単位で Create→gRPC Execute→Remove する。</para>
/// <para>
/// v1 の <see cref="DockerSandboxOptions.ActionRuntimeProfile"/> は <c>dotnet-8.0</c> のみ。
/// Engine 内部状態は渡さず、リクエストと入力のみを Host へ委譲する。
/// </para>
/// </remarks>
/// <param name="options">実行ポリシー（Sandbox.Docker 含む）。</param>
/// <param name="docker">Docker クライアント境界。</param>
/// <param name="hostExecutor">一時 BaseUrl 向け Host 実行。</param>
/// <param name="logger">構造化ログ（機密を含めない）。</param>
internal sealed class DockerSandboxRuntime(
    IOptions<ExecutionPolicyOptions> options,
    IDockerContainerClient docker,
    IEphemeralActionHostExecutor hostExecutor,
    ILogger<DockerSandboxRuntime> logger) : IActionSandboxRuntime
{
    private const string SandboxRuntimeUnavailableCode = "SandboxRuntimeUnavailable";

    /// <inheritdoc />
    public string ProviderKey => "docker";

    /// <inheritdoc />
    public async Task<ActionExecutionResult> RunAsync(
        ActionExecutionRequest request,
        SandboxLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(limits);

        var dockerOptions = options.Value.Sandbox.Docker;
        if (!IsSupportedProfile(dockerOptions.ActionRuntimeProfile))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                $"Unsupported ActionRuntimeProfile '{dockerOptions.ActionRuntimeProfile}'. v1 allows '{DockerSandboxOptions.DefaultActionRuntimeProfile}' only.");
        }

        if (string.IsNullOrWhiteSpace(dockerOptions.Image))
        {
            return Failure(SandboxRuntimeUnavailableCode, "Docker sandbox image is not configured.");
        }

        if (string.Equals(dockerOptions.NetworkMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                SandboxRuntimeUnavailableCode,
                "Docker NetworkMode 'none' is not supported in v1 because host-to-container gRPC requires connectivity.");
        }

        var timeout = limits.Timeout
            ?? TimeSpan.FromSeconds(Math.Max(1, dockerOptions.DefaultTimeoutSeconds));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        DockerStartedContainer? started = null;
        try
        {
            started = await docker.StartActionHostContainerAsync(
                new DockerContainerStartRequest(
                    dockerOptions.Image.Trim(),
                    string.IsNullOrWhiteSpace(dockerOptions.NetworkMode) ? "bridge" : dockerOptions.NetworkMode.Trim(),
                    limits.CpuLimit,
                    limits.MemoryLimitMiB,
                    dockerOptions.ModulesHostPath,
                    string.IsNullOrWhiteSpace(dockerOptions.ModulesContainerPath)
                        ? DockerSandboxOptions.DefaultModulesContainerPath
                        : dockerOptions.ModulesContainerPath.Trim(),
                    dockerOptions.GrpcPort > 0 ? dockerOptions.GrpcPort : DockerSandboxOptions.DefaultGrpcPort),
                timeoutCts.Token).ConfigureAwait(false);

            DockerSandboxRuntimeLog.ContainerStarted(logger, started.ContainerId);
            return await hostExecutor.ExecuteAsync(started.BaseUrl, request, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("SandboxTimeout", "Docker sandbox execution timed out.");
        }
        catch (OperationCanceledException)
        {
            return Failure("Cancelled", "Docker sandbox execution was cancelled.");
        }
#pragma warning disable CA1031 // サンドボックス障害はホストを落とさず Failed に正規化する
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            DockerSandboxRuntimeLog.ContainerFailed(logger, ex);
            return Failure(SandboxRuntimeUnavailableCode, "Docker sandbox runtime is unavailable.");
        }
        finally
        {
            if (started is not null)
            {
                try
                {
                    await docker.StopAndRemoveAsync(started.ContainerId, CancellationToken.None)
                        .ConfigureAwait(false);
                    DockerSandboxRuntimeLog.ContainerRemoved(logger, started.ContainerId);
                }
#pragma warning disable CA1031 // クリーンアップ失敗は実行結果に影響させない
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    DockerSandboxRuntimeLog.ContainerCleanupFailed(logger, ex, started.ContainerId);
                }
            }
        }
    }

    private static bool IsSupportedProfile(string? profile) =>
        string.Equals(
            string.IsNullOrWhiteSpace(profile) ? DockerSandboxOptions.DefaultActionRuntimeProfile : profile.Trim(),
            DockerSandboxOptions.DefaultActionRuntimeProfile,
            StringComparison.Ordinal);

    private static ActionExecutionResult Failure(string errorCode, string message) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
        };
}

/// <summary><see cref="DockerSandboxRuntime"/> の構造化ログ。</summary>
internal static partial class DockerSandboxRuntimeLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Started Docker sandbox container {ContainerId}")]
    public static partial void ContainerStarted(ILogger logger, string containerId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Removed Docker sandbox container {ContainerId}")]
    public static partial void ContainerRemoved(ILogger logger, string containerId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Docker sandbox execution failed")]
    public static partial void ContainerFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Failed to clean up Docker sandbox container {ContainerId}")]
    public static partial void ContainerCleanupFailed(ILogger logger, Exception exception, string containerId);
}

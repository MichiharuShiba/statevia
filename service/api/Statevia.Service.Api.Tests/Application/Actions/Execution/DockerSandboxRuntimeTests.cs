using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="DockerSandboxRuntime"/> の単体テスト（fake Docker / Host）。</summary>
public sealed class DockerSandboxRuntimeTests
{
    /// <summary>正常系: コンテナ起動→Host 委譲→破棄の順で実行する。</summary>
    [Fact]
    public async Task RunAsync_WhenConfigured_StartsExecutesAndRemovesContainer()
    {
        // Arrange
        var docker = new FakeDockerContainerClient();
        var host = new FakeEphemeralActionHostExecutor
        {
            Result = new ActionExecutionResult { Success = true },
        };
        var sut = CreateSut(docker, host, CreateDockerOptions());

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(0.5, 256, TimeSpan.FromSeconds(30)), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, docker.StartCount);
        Assert.Equal(1, docker.StopCount);
        Assert.NotNull(docker.LastStartRequest);
        Assert.Equal(0.5, docker.LastStartRequest!.CpuLimit);
        Assert.Equal(256, docker.LastStartRequest.MemoryLimitMiB);
        Assert.Equal("bridge", docker.LastStartRequest.NetworkMode);
        Assert.Equal("/host/modules", docker.LastStartRequest.ModulesHostPath);
        Assert.Equal(host.LastBaseUrl, docker.StartedBaseUrl);
    }

    /// <summary>Image 未設定は SandboxRuntimeUnavailable。</summary>
    [Fact]
    public async Task RunAsync_WhenImageMissing_ReturnsUnavailable()
    {
        // Arrange
        var dockerOptions = CreateDockerOptions();
        dockerOptions.Image = null;
        var docker = new FakeDockerContainerClient();
        var sut = CreateSut(docker, new FakeEphemeralActionHostExecutor(), dockerOptions);

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, null), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeUnavailable", result.ErrorCode);
        Assert.Equal(0, docker.StartCount);
    }

    /// <summary>未知の ActionRuntimeProfile は拒否する。</summary>
    [Fact]
    public async Task RunAsync_WhenUnsupportedProfile_ReturnsUnavailable()
    {
        // Arrange
        var dockerOptions = CreateDockerOptions();
        dockerOptions.ActionRuntimeProfile = "python-3.12";
        var docker = new FakeDockerContainerClient();
        var sut = CreateSut(docker, new FakeEphemeralActionHostExecutor(), dockerOptions);

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, null), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeUnavailable", result.ErrorCode);
        Assert.Equal(0, docker.StartCount);
    }

    /// <summary>NetworkMode none は v1 非対応として拒否する。</summary>
    [Fact]
    public async Task RunAsync_WhenNetworkModeNone_ReturnsUnavailable()
    {
        // Arrange
        var dockerOptions = CreateDockerOptions();
        dockerOptions.NetworkMode = "none";
        var docker = new FakeDockerContainerClient();
        var sut = CreateSut(docker, new FakeEphemeralActionHostExecutor(), dockerOptions);

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, null), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeUnavailable", result.ErrorCode);
        Assert.Equal(0, docker.StartCount);
    }

    /// <summary>Docker 起動失敗は SandboxRuntimeUnavailable に正規化し、ホストは落とさない。</summary>
    [Fact]
    public async Task RunAsync_WhenDockerStartThrows_ReturnsUnavailable()
    {
        // Arrange
        var docker = new FakeDockerContainerClient { ThrowOnStart = true };
        var sut = CreateSut(docker, new FakeEphemeralActionHostExecutor(), CreateDockerOptions());

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, TimeSpan.FromSeconds(5)), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeUnavailable", result.ErrorCode);
        Assert.Equal(0, docker.StopCount);
    }

    /// <summary>タイムアウトは SandboxTimeout とし、起動済みなら破棄する。</summary>
    [Fact]
    public async Task RunAsync_WhenTimedOut_ReturnsSandboxTimeoutAndRemoves()
    {
        // Arrange
        var docker = new FakeDockerContainerClient();
        var host = new FakeEphemeralActionHostExecutor
        {
            Delay = TimeSpan.FromSeconds(2),
            Result = new ActionExecutionResult { Success = true },
        };
        var sut = CreateSut(docker, host, CreateDockerOptions());

        // Act
        var result = await sut.RunAsync(
            CreateRequest(),
            new SandboxLimits(null, null, TimeSpan.FromMilliseconds(50)),
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxTimeout", result.ErrorCode);
        Assert.Equal(1, docker.StopCount);
    }

    /// <summary>呼び出し元キャンセルは Cancelled を返す。</summary>
    [Fact]
    public async Task RunAsync_WhenCancelled_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var docker = new FakeDockerContainerClient();
        var host = new FakeEphemeralActionHostExecutor { Delay = TimeSpan.FromSeconds(30) };
        var sut = CreateSut(docker, host, CreateDockerOptions());
        await cts.CancelAsync();

        // Act
        var result = await sut.RunAsync(
            CreateRequest(),
            new SandboxLimits(null, null, TimeSpan.FromSeconds(60)),
            cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.ErrorCode);
    }

    /// <summary>破棄失敗でも実行結果は維持する。</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupFails_StillReturnsSuccess()
    {
        // Arrange
        var docker = new FakeDockerContainerClient { ThrowOnStop = true };
        var host = new FakeEphemeralActionHostExecutor
        {
            Result = new ActionExecutionResult { Success = true },
        };
        var sut = CreateSut(docker, host, CreateDockerOptions());

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, TimeSpan.FromSeconds(5)), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, docker.StopCount);
    }

    /// <summary>空白 NetworkMode / GrpcPort 0 は既定値へフォールバックする。</summary>
    [Fact]
    public async Task RunAsync_WhenOptionalDockerFieldsEmpty_AppliesDefaults()
    {
        // Arrange
        var docker = new FakeDockerContainerClient();
        var host = new FakeEphemeralActionHostExecutor
        {
            Result = new ActionExecutionResult { Success = true },
        };
        var dockerOptions = CreateDockerOptions();
        dockerOptions.NetworkMode = "   ";
        dockerOptions.ModulesContainerPath = "   ";
        dockerOptions.GrpcPort = 0;
        dockerOptions.DefaultTimeoutSeconds = 45;
        var sut = CreateSut(docker, host, dockerOptions);

        // Act
        var result = await sut.RunAsync(CreateRequest(), new SandboxLimits(null, null, null), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(docker.LastStartRequest);
        Assert.Equal("bridge", docker.LastStartRequest!.NetworkMode);
        Assert.Equal(DockerSandboxOptions.DefaultModulesContainerPath, docker.LastStartRequest.ModulesContainerPath);
        Assert.Equal(DockerSandboxOptions.DefaultGrpcPort, docker.LastStartRequest.ContainerGrpcPort);
    }

    private static DockerSandboxRuntime CreateSut(
        IDockerContainerClient docker,
        IEphemeralActionHostExecutor host,
        DockerSandboxOptions dockerOptions)
    {
        var options = Options.Create(new ExecutionPolicyOptions
        {
            Sandbox = new SandboxOptions
            {
                ContainerProvider = "docker",
                Docker = dockerOptions,
            },
        });
        return new DockerSandboxRuntime(options, docker, host, NullLogger<DockerSandboxRuntime>.Instance);
    }

    private static DockerSandboxOptions CreateDockerOptions() =>
        new()
        {
            Image = "statevia/action-host:test",
            ActionRuntimeProfile = DockerSandboxOptions.DefaultActionRuntimeProfile,
            NetworkMode = "bridge",
            ModulesHostPath = "/host/modules",
            ModulesContainerPath = "/app/modules",
            GrpcPort = 5001,
        };

    private static ActionExecutionRequest CreateRequest() =>
        new()
        {
            ExecutionId = "exec-docker-1",
            StateName = "A",
            ActionId = "test.module.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

    private sealed class FakeDockerContainerClient : IDockerContainerClient
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public bool ThrowOnStart { get; init; }

        public bool ThrowOnStop { get; init; }

        public DockerContainerStartRequest? LastStartRequest { get; private set; }

        public string StartedBaseUrl { get; } = "http://127.0.0.1:18080";

        public Task<DockerStartedContainer> StartActionHostContainerAsync(
            DockerContainerStartRequest request,
            CancellationToken cancellationToken)
        {
            StartCount++;
            LastStartRequest = request;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("simulated docker failure");
            }

            return Task.FromResult(new DockerStartedContainer("cid-1", StartedBaseUrl));
        }

        public Task StopAndRemoveAsync(string containerId, CancellationToken cancellationToken)
        {
            StopCount++;
            if (ThrowOnStop)
            {
                throw new InvalidOperationException("simulated cleanup failure");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeEphemeralActionHostExecutor : IEphemeralActionHostExecutor
    {
        public ActionExecutionResult Result { get; init; } = new() { Success = true };

        public TimeSpan Delay { get; init; }

        public string? LastBaseUrl { get; private set; }

        public async Task<ActionExecutionResult> ExecuteAsync(
            string baseUrl,
            ActionExecutionRequest request,
            CancellationToken cancellationToken)
        {
            LastBaseUrl = baseUrl;
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }

            return Result;
        }
    }
}

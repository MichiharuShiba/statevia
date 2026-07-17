using System.Reflection;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="DockerDotNetContainerClient"/> の単体テスト（Docker 非依存部分とポート解決）。</summary>
public sealed class DockerDotNetContainerClientTests
{
    /// <summary>Image 未指定は Docker 呼び出し前に失敗する。</summary>
    [Fact]
    public async Task StartActionHostContainerAsync_WhenImageMissing_Throws()
    {
        // Arrange
        var sut = CreateSut();
        var request = new DockerContainerStartRequest(
            Image: " ",
            NetworkMode: "bridge",
            CpuLimit: null,
            MemoryLimitMiB: null,
            ModulesHostPath: null,
            ModulesContainerPath: "/app/modules",
            ContainerGrpcPort: 5001);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.StartActionHostContainerAsync(request, CancellationToken.None));
    }

    /// <summary>containerId が空なら Stop は no-op とする。</summary>
    [Fact]
    public async Task StopAndRemoveAsync_WhenContainerIdEmpty_CompletesWithoutDocker()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            sut.StopAndRemoveAsync("  ", CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>公開ポートが解決できるとき HostPort を返す。</summary>
    [Fact]
    public void ResolvePublishedHostPort_WhenBindingPresent_ReturnsHostPort()
    {
        // Arrange
        var inspect = new ContainerInspectResponse
        {
            NetworkSettings = new NetworkSettings
            {
                Ports = new Dictionary<string, IList<PortBinding>>
                {
                    ["5001/tcp"] =
                    [
                        new PortBinding { HostIP = "127.0.0.1", HostPort = "49152" },
                    ],
                },
            },
        };

        // Act
        var port = InvokeResolvePublishedHostPort(inspect, "5001/tcp");

        // Assert
        Assert.Equal("49152", port);
    }

    /// <summary>Ports が null のときは解決できない。</summary>
    [Fact]
    public void ResolvePublishedHostPort_WhenPortsNull_ReturnsNull()
    {
        // Arrange
        var inspect = new ContainerInspectResponse { NetworkSettings = null };

        // Act
        var port = InvokeResolvePublishedHostPort(inspect, "5001/tcp");

        // Assert
        Assert.Null(port);
    }

    /// <summary>対象キーが無いときは解決できない。</summary>
    [Fact]
    public void ResolvePublishedHostPort_WhenKeyMissing_ReturnsNull()
    {
        // Arrange
        var inspect = new ContainerInspectResponse
        {
            NetworkSettings = new NetworkSettings
            {
                Ports = new Dictionary<string, IList<PortBinding>>(),
            },
        };

        // Act
        var port = InvokeResolvePublishedHostPort(inspect, "5001/tcp");

        // Assert
        Assert.Null(port);
    }

    private static DockerDotNetContainerClient CreateSut() =>
        new(Options.Create(new ExecutionPolicyOptions
        {
            Sandbox = new SandboxOptions { Docker = new DockerSandboxOptions() },
        }));

    private static string? InvokeResolvePublishedHostPort(ContainerInspectResponse inspect, string grpcPortKey)
    {
        var method = typeof(DockerDotNetContainerClient).GetMethod(
            "ResolvePublishedHostPort",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, [inspect, grpcPortKey]);
    }
}

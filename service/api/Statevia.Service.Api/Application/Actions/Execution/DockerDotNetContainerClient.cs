using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Docker.DotNet を用いた <see cref="IDockerContainerClient"/> 実装。</summary>
/// <remarks>
/// <para>SDK 依存を本クラスに封じ込める。上位は <see cref="IDockerContainerClient"/> のみに依存する。</para>
/// <para>認証情報・Endpoint 詳細はログへ出さない。</para>
/// </remarks>
internal sealed class DockerDotNetContainerClient(IOptions<ExecutionPolicyOptions> options) : IDockerContainerClient
{
    private readonly DockerSandboxOptions _docker = options.Value.Sandbox.Docker;

    /// <inheritdoc />
    public async Task<DockerStartedContainer> StartActionHostContainerAsync(
        DockerContainerStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Image);

        using var client = CreateClient();
        var grpcPortKey = $"{request.ContainerGrpcPort}/tcp";
        var env = new List<string>
        {
            $"ASPNETCORE_URLS=http://+:{request.ContainerGrpcPort}",
            $"STATEVIA_MODULES_PATH={request.ModulesContainerPath.TrimEnd('/')}",
        };

        var hostConfig = new HostConfig
        {
            NetworkMode = string.IsNullOrWhiteSpace(request.NetworkMode) ? "bridge" : request.NetworkMode,
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                [grpcPortKey] =
                [
                    new PortBinding
                    {
                        HostIP = "127.0.0.1",
                        HostPort = "0",
                    },
                ],
            },
        };

        if (request.MemoryLimitMiB is { } memoryMiB and > 0)
        {
            hostConfig.Memory = memoryMiB * 1024L * 1024L;
        }

        if (request.CpuLimit is { } cpu and > 0)
        {
            hostConfig.NanoCPUs = (long)(cpu * 1_000_000_000d);
        }

        if (!string.IsNullOrWhiteSpace(request.ModulesHostPath))
        {
            var hostPath = Path.GetFullPath(request.ModulesHostPath);
            hostConfig.Binds =
            [
                $"{hostPath}:{request.ModulesContainerPath.TrimEnd('/')}:ro",
            ];
        }

        var create = await client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = request.Image,
                Env = env,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    [grpcPortKey] = default,
                },
                HostConfig = hostConfig,
            },
            cancellationToken).ConfigureAwait(false);

        var started = await client.Containers.StartContainerAsync(
            create.ID,
            new ContainerStartParameters(),
            cancellationToken).ConfigureAwait(false);

        if (!started)
        {
            await SafeRemoveAsync(client, create.ID, CancellationToken.None).ConfigureAwait(false);
            throw new InvalidOperationException("Failed to start Docker sandbox container.");
        }

        var inspect = await client.Containers.InspectContainerAsync(create.ID, cancellationToken)
            .ConfigureAwait(false);
        var hostPort = ResolvePublishedHostPort(inspect, grpcPortKey)
            ?? throw new InvalidOperationException("Docker sandbox container did not publish a host port.");

        return new DockerStartedContainer(create.ID, $"http://127.0.0.1:{hostPort}");
    }

    /// <inheritdoc />
    public async Task StopAndRemoveAsync(string containerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            return;
        }

        using var client = CreateClient();
        try
        {
            await client.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DockerContainerNotFoundException)
        {
            return;
        }
#pragma warning disable CA1031 // 破棄優先: Stop 失敗・キャンセルでも Remove を試みる
        catch (Exception)
#pragma warning restore CA1031
        {
            // Stop 失敗でも Remove を試みる
        }

        await SafeRemoveAsync(client, containerId, CancellationToken.None).ConfigureAwait(false);
    }

    private DockerClient CreateClient()
    {
        var endpoint = _docker.Endpoint;
        DockerClientConfiguration configuration = string.IsNullOrWhiteSpace(endpoint)
            ? new DockerClientConfiguration()
            : new DockerClientConfiguration(new Uri(endpoint.Trim()));

        try
        {
            return configuration.CreateClient();
        }
        finally
        {
            configuration.Dispose();
        }
    }

    private static string? ResolvePublishedHostPort(ContainerInspectResponse inspect, string grpcPortKey)
    {
        if (inspect.NetworkSettings?.Ports is null)
        {
            return null;
        }

        if (!inspect.NetworkSettings.Ports.TryGetValue(grpcPortKey, out var bindings)
            || bindings is null
            || bindings.Count == 0)
        {
            return null;
        }

        return bindings
            .Select(binding => binding.HostPort)
            .FirstOrDefault(port => !string.IsNullOrWhiteSpace(port));
    }

    private static async Task SafeRemoveAsync(DockerClient client, string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await client.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DockerContainerNotFoundException)
        {
            // already gone
        }
    }
}

extern alias ActionHost;

using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Infrastructure.Actions.Grpc.Contracts;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Application.Actions.Visibility;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using ActionHostOptions = ActionHost::Statevia.Service.ActionHost.ActionHostOptions;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>Core-API と Action Host の OutOfProcess 統合テスト。</summary>
public sealed class OutOfProcessExecutionIntegrationTests : IAsyncLifetime
{
    private readonly string _modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
    private WebApplicationFactory<ActionHost::Program> _actionHostFactory = null!;
    private GrpcChannel _channel = null!;

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _actionHostFactory = new WebApplicationFactory<ActionHost::Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting($"{ActionHostOptions.SectionName}:ModulesPath", _modulesRoot));

        var httpClient = _actionHostFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        _channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = httpClient,
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        _channel.Dispose();
        await _actionHostFactory.DisposeAsync();
    }

    /// <summary>Production Policy の Community Module を Action Host 経由で実行できる。</summary>
    [Fact]
    public async Task ExecuteAsync_CommunityModuleInProduction_UsesActionHost()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "test.module.echo",
                ModuleId = "test.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Community,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Tenant,
                OwnerTenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new EchoState())));

        var grpcClient = new ActionExecutionService.ActionExecutionServiceClient(_channel);
        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton<IActionCatalog>(catalog);
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        services.AddSingleton<IActionExecutionPolicy, ConfigurableExecutionPolicy>();
        services.AddSingleton(Options.Create(new ExecutionPolicyOptions()));
        services.AddSingleton<IActionHostExecutionClient>(
            new GrpcTestActionHostExecutionClient(grpcClient));
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IHostEnvironment>(new ActionExecutionTestSupport.TestHostEnvironment
        {
            EnvironmentName = Environments.Production,
        });
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();

        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-oop-integration",
            StateName = "Echo",
        };
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-oop-integration",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };
        var inputJson = """{"message":"from-core-api"}""";
        using var inputDocument = System.Text.Json.JsonDocument.Parse(inputJson);
        var runtimeInput = inputDocument.RootElement.Clone();

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(inputJson, result.Output?.GetRawText());
    }

    private sealed class EchoState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    private sealed class GrpcTestActionHostExecutionClient(
        ActionExecutionService.ActionExecutionServiceClient client) : IActionHostExecutionClient
    {
        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken) =>
            ActionHostGrpcInvoker.ExecuteAsync(client, request, cancellationToken);
    }

    private static string CreateModuleLayoutFromBuiltAssembly(string moduleDirectoryName)
    {
        var modulesRoot = Path.Combine(Path.GetTempPath(), "statevia-oop-test", Guid.NewGuid().ToString("N"));
        var moduleDirectory = Path.Combine(modulesRoot, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, "TestActionModule.dll");
        var targetPath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        File.Copy(builtAssemblyPath, targetPath, overwrite: true);

        foreach (var dependency in Directory.GetFiles(Path.GetDirectoryName(builtAssemblyPath)!, "*.dll"))
        {
            var dependencyName = Path.GetFileName(dependency);
            if (string.Equals(dependencyName, Path.GetFileName(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(dependency, Path.Combine(moduleDirectory, dependencyName), overwrite: true);
        }

        return modulesRoot;
    }
}

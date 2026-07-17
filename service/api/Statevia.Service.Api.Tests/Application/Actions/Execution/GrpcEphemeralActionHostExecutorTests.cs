extern alias ActionHost;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;
using ActionHostOptions = ActionHost::Statevia.Service.ActionHost.ActionHostOptions;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="GrpcEphemeralActionHostExecutor"/> の統合テスト（in-memory Action Host）。</summary>
public sealed class GrpcEphemeralActionHostExecutorTests : IAsyncLifetime
{
    private readonly string _modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
    private WebApplicationFactory<ActionHost::Program> _actionHostFactory = null!;
    private HttpClient _httpClient = null!;
    private string _baseUrl = null!;

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _actionHostFactory = new WebApplicationFactory<ActionHost::Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting($"{ActionHostOptions.SectionName}:ModulesPath", _modulesRoot));

        _httpClient = _actionHostFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        _baseUrl = _httpClient.BaseAddress!.ToString().TrimEnd('/');

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _actionHostFactory.DisposeAsync();
    }

    /// <summary>短命 BaseUrl 向けに Action Host へ gRPC 実行できる。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionHostRunning_ReturnsSuccess()
    {
        // Arrange
        var sut = new GrpcEphemeralActionHostExecutor(_httpClient);
        using var inputDocument = System.Text.Json.JsonDocument.Parse("""{"message":"ephemeral"}""");
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-ephemeral-1",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            Input = inputDocument.RootElement.Clone(),
        };

        // Act
        var result = await sut.ExecuteAsync(_baseUrl, request, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"{result.ErrorCode}: {result.ErrorMessage}");
    }

    /// <summary>BaseUrl 未指定は引数検証で失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBaseUrlMissing_Throws()
    {
        // Arrange
        var sut = new GrpcEphemeralActionHostExecutor();
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-ephemeral-2",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ExecuteAsync("  ", request, CancellationToken.None));
    }

    private static string CreateModuleLayoutFromBuiltAssembly(string moduleDirectoryName)
    {
        var modulesRoot = Path.Combine(Path.GetTempPath(), "statevia-ephemeral-test", Guid.NewGuid().ToString("N"));
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

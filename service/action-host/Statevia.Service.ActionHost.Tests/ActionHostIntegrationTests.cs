using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Service.ActionHost.Tests;

/// <summary>Action Host の gRPC 実行統合テスト。</summary>
public sealed class ActionExecutionGrpcIntegrationTests : IClassFixture<ActionHostWebApplicationFactory>
{
    private readonly ActionHostWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ActionExecutionGrpcIntegrationTests(ActionHostWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>load 済み Module Action を gRPC で実行できる。</summary>
    [Fact]
    public async Task ExecuteAction_WhenEchoModuleLoaded_ReturnsInputAsOutput()
    {
        // Arrange
        using var channel = _factory.CreateGrpcChannel();
        var client = new ActionExecutionService.ActionExecutionServiceClient(channel);
        var inputJson = """{"message":"hello-host"}""";
        var request = new ActionExecutionRpcRequest
        {
            ExecutionId = "exec-grpc-1",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = "00000000-0000-4000-8000-000000000001",
            InputJson = inputJson,
        };

        // Act
        var response = await client.ExecuteActionAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(inputJson, response.OutputJson);
    }

    /// <summary>未登録 actionId は失敗レスポンスを返す。</summary>
    [Fact]
    public async Task ExecuteAction_WhenUnknownAction_ReturnsFailure()
    {
        // Arrange
        using var channel = _factory.CreateGrpcChannel();
        var client = new ActionExecutionService.ActionExecutionServiceClient(channel);
        var request = new ActionExecutionRpcRequest
        {
            ExecutionId = "exec-grpc-2",
            StateName = "Missing",
            ActionId = "missing.module.action",
            TenantId = "00000000-0000-4000-8000-000000000001",
        };

        // Act
        var response = await client.ExecuteActionAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.Equal("UnknownAction", response.ErrorCode);
    }

    /// <summary>期限切れ deadline は DeadlineExceeded を返す。</summary>
    [Fact]
    public async Task ExecuteAction_WhenDeadlinePassed_ReturnsDeadlineExceeded()
    {
        // Arrange
        using var channel = _factory.CreateGrpcChannel();
        var client = new ActionExecutionService.ActionExecutionServiceClient(channel);
        var request = new ActionExecutionRpcRequest
        {
            ExecutionId = "exec-grpc-deadline",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = "00000000-0000-4000-8000-000000000001",
            DeadlineUnixMs = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
        };

        // Act
        var response = await client.ExecuteActionAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.Equal("DeadlineExceeded", response.ErrorCode);
    }

    /// <summary>ルート GET は稼働メッセージを返す。</summary>
    [Fact]
    public async Task GetRoot_ReturnsHostMessage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var body = await client.GetStringAsync("/");

        // Assert
        Assert.Contains("Statevia Action Host", body, StringComparison.Ordinal);
    }
}

internal static class TestModuleLayout
{
    /// <summary>ビルド済み fixture DLL を modules レイアウトへコピーする。</summary>
    /// <param name="moduleDirectoryName">module ディレクトリ名（DLL 名にも使用）。</param>
    /// <param name="modulesRoot">既存の modules ルート。未指定時は一時ディレクトリを作成する。</param>
    /// <param name="assemblyFileName">コピー元の fixture DLL 名。</param>
    public static string CopyBuiltAssembly(
        string moduleDirectoryName,
        string? modulesRoot = null,
        string assemblyFileName = "TestActionModule.dll")
    {
        modulesRoot ??= CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Bootstrap;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Bootstrap;

/// <summary><see cref="BootstrapApp"/> の検証。</summary>
public sealed class BootstrapAppTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;

    public BootstrapAppTests()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BootstrapApp.ServiceProviderFactory = null;
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }

    /// <summary>引数なしはルートヘルプを表示して 0 を返す。</summary>
    [Fact]
    public async Task RunAsync_NoArgs_ReturnsZeroAndPrintsRootHelp()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);

        // Act
        var exitCode = await BootstrapApp.RunAsync([]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Statevia platform bootstrap CLI", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>未知コマンドは 1 を返す。</summary>
    [Fact]
    public async Task RunAsync_UnknownCommand_ReturnsOne()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(output);

        // Act
        var exitCode = await BootstrapApp.RunAsync(["unknown-cmd"]);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown command", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>create-tenant でテナントキー未指定は 1。</summary>
    [Fact]
    public async Task RunAsync_CreateTenant_MissingKey_ReturnsOne()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(output);
        using var database = new SqliteTestDatabase();
        BootstrapApp.ServiceProviderFactory = () =>
            BootstrapServiceRegistration.BuildProvider(database.Factory);

        // Act
        var exitCode = await BootstrapApp.RunAsync(["create-tenant"]);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("create-tenant", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>create-tenant で新規テナントを作成できる。</summary>
    [Fact]
    public async Task RunAsync_CreateTenant_CreatesRow()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);
        using var database = new SqliteTestDatabase();
        BootstrapApp.ServiceProviderFactory = () =>
            BootstrapServiceRegistration.BuildProvider(database.Factory);

        // Act
        var exitCode = await BootstrapApp.RunAsync([
            "create-tenant",
            "--tenant-key", "cli-acme",
            "--display-name", "CLI Acme"]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Created tenant", output.ToString(), StringComparison.Ordinal);
        await using var db = database.Factory.CreateDbContext();
        Assert.True(await db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantKey == "cli-acme"));
    }

    /// <summary>create-admin でパスワード未設定は 1。</summary>
    [Fact]
    public async Task RunAsync_CreateAdmin_MissingPassword_ReturnsOne()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(output);
        using var database = new SqliteTestDatabase();
        BootstrapApp.ServiceProviderFactory = () =>
            BootstrapServiceRegistration.BuildProvider(database.Factory);
        Environment.SetEnvironmentVariable("STATEVIA_BOOTSTRAP_PASSWORD", null);

        // Act
        var exitCode = await BootstrapApp.RunAsync([
            "create-admin",
            "--email", "admin@example.com"]);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("STATEVIA_BOOTSTRAP_PASSWORD", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>create-admin で管理者を作成できる。</summary>
    [Fact]
    public async Task RunAsync_CreateAdmin_CreatesAdmin()
    {
        // Arrange
        using var output = new StringWriter();
        Console.SetOut(output);
        using var database = new SqliteTestDatabase();
        BootstrapApp.ServiceProviderFactory = () =>
            BootstrapServiceRegistration.BuildProvider(database.Factory);

        // Act
        var exitCode = await BootstrapApp.RunAsync([
            "create-admin",
            "--email", "cli-admin@example.com",
            "--password", "cli-test-password-32chars!!"]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Created tenant admin", output.ToString(), StringComparison.Ordinal);
    }
}

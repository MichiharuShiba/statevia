using Statevia.Service.Api.Bootstrap;

namespace Statevia.Service.Api.Tests.Bootstrap;

/// <summary><see cref="BootstrapHelp"/> の検証。</summary>
public sealed class BootstrapHelpTests
{
    /// <summary>ルートヘルプにコマンド一覧が含まれる。</summary>
    [Fact]
    public async Task WriteRootAsync_IncludesCommands()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        await BootstrapHelp.WriteRootAsync(writer);

        // Assert
        var text = writer.ToString();
        Assert.Contains("create-tenant", text, StringComparison.Ordinal);
        Assert.Contains("create-admin", text, StringComparison.Ordinal);
    }

    /// <summary>create-tenant ヘルプに tenant-key が含まれる。</summary>
    [Fact]
    public async Task WriteCreateTenantAsync_IncludesTenantKeyOption()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        await BootstrapHelp.WriteCreateTenantAsync(writer);

        // Assert
        Assert.Contains("--tenant-key", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>create-admin ヘルプに username が含まれる。</summary>
    [Fact]
    public async Task WriteCreateAdminAsync_IncludesUsernameOption()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        await BootstrapHelp.WriteCreateAdminAsync(writer);

        // Assert
        Assert.Contains("--username", writer.ToString(), StringComparison.Ordinal);
    }
}

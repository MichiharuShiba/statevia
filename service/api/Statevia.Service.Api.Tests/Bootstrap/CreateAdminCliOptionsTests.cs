using Statevia.Service.Api.Bootstrap;

namespace Statevia.Service.Api.Tests.Bootstrap;

/// <summary><see cref="CreateAdminCliOptions"/> の検証。</summary>
public sealed class CreateAdminCliOptionsTests
{
    /// <summary>必須オプションを解析する。</summary>
    [Fact]
    public void Parse_ValidArgs_ParsesOptions()
    {
        // Act
        var options = CreateAdminCliOptions.Parse([
            "--tenant-key", "acme",
            "--username", "admin",
            "--password", "secret",
            "--display-name", "Admin",
            "--skip-if-exists"]);

        // Assert
        Assert.False(options.ShowHelp);
        Assert.Equal("acme", options.TenantKey);
        Assert.Equal("admin", options.Username);
        Assert.Equal("secret", options.Password);
        Assert.Equal("Admin", options.DisplayName);
        Assert.True(options.SkipIfExists);
    }

    /// <summary>ユーザー名未指定はヘルプ表示。</summary>
    [Fact]
    public void Parse_MissingUsername_ShowsHelp()
    {
        // Act
        var options = CreateAdminCliOptions.Parse(["--password", "secret"]);

        // Assert
        Assert.True(options.ShowHelp);
    }

    /// <summary>テナントキー省略時は default。</summary>
    [Fact]
    public void Parse_OmitsTenantKey_UsesDefault()
    {
        // Act
        var options = CreateAdminCliOptions.Parse(["--username", "admin", "--password", "p"]);

        // Assert
        Assert.Equal(TenantRequestHeaders.DefaultTenantId, options.TenantKey);
    }
}

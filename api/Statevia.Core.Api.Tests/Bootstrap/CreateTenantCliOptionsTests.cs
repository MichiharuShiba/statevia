using Statevia.Core.Api.Bootstrap;

namespace Statevia.Core.Api.Tests.Bootstrap;

/// <summary><see cref="CreateTenantCliOptions"/> の検証。</summary>
public sealed class CreateTenantCliOptionsTests
{
    /// <summary>必須オプションを解析する。</summary>
    [Fact]
    public void Parse_ValidArgs_ParsesOptions()
    {
        // Act
        var options = CreateTenantCliOptions.Parse([
            "--tenant-key", "acme-corp",
            "--display-name", "Acme",
            "--skip-if-exists"]);

        // Assert
        Assert.False(options.ShowHelp);
        Assert.Equal("acme-corp", options.TenantKey);
        Assert.Equal("Acme", options.DisplayName);
        Assert.True(options.SkipIfExists);
    }

    /// <summary>テナントキー未指定はヘルプ表示。</summary>
    [Fact]
    public void Parse_MissingTenantKey_ShowsHelp()
    {
        // Act
        var options = CreateTenantCliOptions.Parse([]);

        // Assert
        Assert.True(options.ShowHelp);
        Assert.False(options.IsHelpOnly);
    }

    /// <summary>--help はヘルプのみ。</summary>
    [Fact]
    public void Parse_HelpOnly_SetsIsHelpOnly()
    {
        // Act
        var options = CreateTenantCliOptions.Parse(["--help"]);

        // Assert
        Assert.True(options.ShowHelp);
        Assert.True(options.IsHelpOnly);
    }

    /// <summary>未知の引数はヘルプ表示。</summary>
    [Fact]
    public void Parse_UnknownArg_ShowsHelp()
    {
        // Act
        var options = CreateTenantCliOptions.Parse(["--unknown"]);

        // Assert
        Assert.True(options.ShowHelp);
    }
}

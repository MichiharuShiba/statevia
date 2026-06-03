using Statevia.Core.Api.Bootstrap;

namespace Statevia.Core.Api.Tests.Bootstrap;

/// <summary><see cref="BootstrapGlobalCliOptions"/> の検証。</summary>
public sealed class BootstrapGlobalCliOptionsTests
{
    /// <summary>グローバルオプションを除去してコマンド引数を残す。</summary>
    [Fact]
    public void Parse_GlobalOptionsBeforeCommand_SplitsArgs()
    {
        // Act
        var (global, commandArgs) = BootstrapGlobalCliOptions.Parse([
            "--database-url", "Host=test;Database=db",
            "--config", "custom.json",
            "create-tenant",
            "--tenant-key", "acme"]);

        // Assert
        Assert.Equal("Host=test;Database=db", global.DatabaseUrl);
        Assert.Equal("custom.json", global.ConfigPath);
        Assert.Equal(["create-tenant", "--tenant-key", "acme"], commandArgs);
    }

    /// <summary>--connection-string は --database-url と同義。</summary>
    [Fact]
    public void Parse_ConnectionStringAlias_SetsDatabaseUrl()
    {
        // Act
        var (global, _) = BootstrapGlobalCliOptions.Parse([
            "--connection-string", "postgres://u:p@host/db",
            "create-admin"]);

        // Assert
        Assert.Equal("postgres://u:p@host/db", global.DatabaseUrl);
    }

    /// <summary>グローバルオプションのみのときコマンド引数は空。</summary>
    [Fact]
    public void Parse_OnlyGlobalOptions_CommandArgsEmpty()
    {
        // Act
        var (global, commandArgs) = BootstrapGlobalCliOptions.Parse(["--help"]);

        // Assert
        Assert.True(global.ShowRootHelp);
        Assert.Empty(commandArgs);
    }
}

using Statevia.Service.Cli.Infrastructure;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary>ホーム config / secrets ストアの単体テスト。</summary>
public sealed class CliConfigStoreTests
{
    /// <summary>config の読み書きがホーム配下に閉じる。</summary>
    [Fact]
    public void CliConfigStore_RoundTrip_WritesHomeConfig()
    {
        // Arrange
        var home = CreateHome();
        var store = new CliConfigStore(home);

        // Act
        store.Save(new CliConfigFile("http://localhost:8080/", "default", "./modules"));
        var loaded = store.TryLoad();

        // Assert
        Assert.Equal(Path.Combine(home.HomeDirectory, "config"), store.FilePath);
        Assert.NotNull(loaded);
        Assert.Equal("http://localhost:8080/", loaded.ApiBase);
        Assert.Equal("default", loaded.Tenant);
        Assert.Equal("./modules", loaded.ModulesPath);
    }

    /// <summary>未知プロパティは無視する。</summary>
    [Fact]
    public void CliConfigStore_UnknownProperty_IsIgnored()
    {
        // Arrange
        var home = CreateHome();
        Directory.CreateDirectory(home.HomeDirectory);
        File.WriteAllText(
            home.ConfigPath,
            """{"apiBase":"http://localhost:8080/","extra":"ignored"}""");

        // Act
        var loaded = new CliConfigStore(home).TryLoad();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("http://localhost:8080/", loaded.ApiBase);
        Assert.Null(loaded.Tenant);
    }

    /// <summary>不正 JSON は未設定にせず例外にする。</summary>
    [Fact]
    public void CliConfigStore_InvalidJson_Throws()
    {
        // Arrange
        var home = CreateHome();
        Directory.CreateDirectory(home.HomeDirectory);
        File.WriteAllText(home.ConfigPath, "{not-json");

        // Act
        var ex = Assert.Throws<CliHomeFileException>(() => new CliConfigStore(home).TryLoad());

        // Assert
        Assert.Equal(home.ConfigPath, ex.FilePath);
        Assert.DoesNotContain("{not-json", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>作業ディレクトリ配下の config は読まない。</summary>
    [Fact]
    public void CliConfigStore_IgnoresConfigUnderCurrentDirectory()
    {
        // Arrange
        var home = CreateHome();
        var previousCwd = Directory.GetCurrentDirectory();
        var cwd = Path.Combine(Path.GetTempPath(), "statevia-cli-cwd", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(cwd, ".statevia"));
        File.WriteAllText(
            Path.Combine(cwd, ".statevia", "config"),
            """{"tenant":"from-cwd"}""");

        try
        {
            Directory.SetCurrentDirectory(cwd);

            // Act
            var loaded = new CliConfigStore(home).TryLoad();

            // Assert
            Assert.Null(loaded);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }

    /// <summary>secrets に保存した API キーをホームから読める。</summary>
    [Fact]
    public void CliSecretsStore_Save_RoundTripsApiKey()
    {
        // Arrange
        var home = CreateHome();
        var store = new CliSecretsStore(home);

        // Act
        store.SaveApiKey("home-secret-key");

        // Assert
        Assert.Equal("home-secret-key", store.TryGetApiKey());
        if (!OperatingSystem.IsWindows())
        {
            var fileMode = File.GetUnixFileMode(store.FilePath);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
        }
    }

    /// <summary>作業ディレクトリ配下の secrets は読まない。</summary>
    [Fact]
    public void CliSecretsStore_IgnoresSecretsUnderCurrentDirectory()
    {
        // Arrange
        var home = CreateHome();
        var previousCwd = Directory.GetCurrentDirectory();
        var cwd = Path.Combine(Path.GetTempPath(), "statevia-cli-cwd", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(cwd, ".statevia"));
        File.WriteAllText(
            Path.Combine(cwd, ".statevia", "secrets"),
            """{"apiKey":"from-cwd"}""");

        try
        {
            Directory.SetCurrentDirectory(cwd);

            // Act
            var loaded = new CliSecretsStore(home).TryGetApiKey();

            // Assert
            Assert.Null(loaded);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }

    /// <summary>空の API キーは保存できない。</summary>
    [Fact]
    public void CliSecretsStore_SaveEmpty_Throws()
    {
        // Arrange
        var store = new CliSecretsStore(CreateHome());

        // Act
        var ex = Assert.Throws<ArgumentException>(() => store.SaveApiKey("  "));

        // Assert
        Assert.Equal("apiKey", ex.ParamName);
    }

    /// <summary>secrets の不正 JSON は未設定にせず例外にする。</summary>
    [Fact]
    public void CliSecretsStore_InvalidJson_Throws()
    {
        // Arrange
        var home = CreateHome();
        Directory.CreateDirectory(home.HomeDirectory);
        File.WriteAllText(home.SecretsPath, "{not-json");
        var store = new CliSecretsStore(home);

        // Act
        var ex = Assert.Throws<CliHomeFileException>(store.TryGetApiKey);

        // Assert
        Assert.Equal(home.SecretsPath, ex.FilePath);
        Assert.DoesNotContain("{not-json", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>DeleteApiKey は secrets ファイルを削除する。</summary>
    [Fact]
    public void CliSecretsStore_DeleteApiKey_RemovesFile()
    {
        // Arrange
        var store = new CliSecretsStore(CreateHome());
        store.SaveApiKey("to-delete");

        // Act
        store.DeleteApiKey();

        // Assert
        Assert.False(File.Exists(store.FilePath));
        Assert.Null(store.TryGetApiKey());
    }

    /// <summary>空の config を保存するとファイルを削除する。</summary>
    [Fact]
    public void CliConfigStore_SaveEmpty_DeletesFile()
    {
        // Arrange
        var home = CreateHome();
        var store = new CliConfigStore(home);
        store.Save(new CliConfigFile(Tenant: "default"));

        // Act
        store.Save(new CliConfigFile());

        // Assert
        Assert.False(File.Exists(store.FilePath));
    }

    private static CliHomePaths CreateHome()
    {
        var directory = Path.Combine(Path.GetTempPath(), "statevia-cli-home", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new CliHomePaths(directory);
    }
}

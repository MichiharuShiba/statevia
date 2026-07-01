using Microsoft.Extensions.Configuration;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary><see cref="DatabaseConnection"/> の接続文字列解決テスト。</summary>
public sealed class DatabaseConnectionTests
{
    /// <summary>設定の DefaultConnection を使う。</summary>
    [Fact]
    public void Resolve_UsesConnectionStringFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=cfg;Database=db;Username=u;Password=p"
            })
            .Build();

        // Act
        var connectionString = DatabaseConnection.Resolve(config);

        // Assert
        Assert.Contains("Host=cfg", connectionString, StringComparison.Ordinal);
    }

    /// <summary>DATABASE_URL が設定されているとき正規化して優先する。</summary>
    [Fact]
    public void Resolve_UsesNormalizedDatabaseUrlEnv()
    {
        // Arrange
        const string url = "postgres://user:secret@db.example:5433/statevia";
        Environment.SetEnvironmentVariable("DATABASE_URL", url);
        try
        {
            var config = new ConfigurationBuilder().Build();

            // Act
            var connectionString = DatabaseConnection.Resolve(config);

            // Assert
            Assert.Contains("Host=db.example", connectionString, StringComparison.Ordinal);
            Assert.Contains("Port=5433", connectionString, StringComparison.Ordinal);
            Assert.Contains("Database=statevia", connectionString, StringComparison.Ordinal);
            Assert.Contains("Username=user", connectionString, StringComparison.Ordinal);
            Assert.Contains("Password=secret", connectionString, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
        }
    }

    /// <summary>設定も環境変数も無いとき既定のローカル接続文字列を返す。</summary>
    [Fact]
    public void Resolve_FallsBackToLocalDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        var config = new ConfigurationBuilder().Build();

        // Act
        var connectionString = DatabaseConnection.Resolve(config);

        // Assert
        Assert.Contains("Host=localhost", connectionString, StringComparison.Ordinal);
        Assert.Contains("Database=statevia", connectionString, StringComparison.Ordinal);
    }

    /// <summary>postgres:// 以外の URL はそのまま返す。</summary>
    [Fact]
    public void NormalizePostgresUrl_ReturnsOriginal_WhenNotPostgresScheme()
    {
        // Arrange
        const string url = "Host=already;Database=npg";

        // Act
        var normalized = DatabaseConnection.NormalizePostgresUrl(url);

        // Assert
        Assert.Equal(url, normalized);
    }

    /// <summary>postgresql:// スキームも正規化する。</summary>
    [Fact]
    public void NormalizePostgresUrl_ParsesPostgresqlScheme()
    {
        // Arrange
        const string url = "postgresql://u:p@host/dbname";

        // Act
        var normalized = DatabaseConnection.NormalizePostgresUrl(url);

        // Assert
        Assert.Contains("Host=host", normalized, StringComparison.Ordinal);
        Assert.Contains("Database=dbname", normalized, StringComparison.Ordinal);
        Assert.Contains("Username=u", normalized, StringComparison.Ordinal);
        Assert.Contains("Password=p", normalized, StringComparison.Ordinal);
    }

    /// <summary>ユーザー情報が無い URL でもホストと DB 名を解決する。</summary>
    [Fact]
    public void NormalizePostgresUrl_WorksWithoutUserInfo()
    {
        // Arrange
        const string url = "postgres://localhost/mydb";

        // Act
        var normalized = DatabaseConnection.NormalizePostgresUrl(url);

        // Assert
        Assert.Contains("Host=localhost", normalized, StringComparison.Ordinal);
        Assert.Contains("Database=mydb", normalized, StringComparison.Ordinal);
        Assert.Contains("Username=", normalized, StringComparison.Ordinal);
    }

    /// <summary>CLI オーバーライドは環境変数より優先する。</summary>
    [Fact]
    public void Resolve_ConnectionStringOverride_TakesPrecedenceOverEnv()
    {
        // Arrange
        Environment.SetEnvironmentVariable("DATABASE_URL", "postgres://env:env@ignored/db");
        try
        {
            var config = new ConfigurationBuilder().Build();
            const string overrideValue = "Host=cli;Database=statevia;Username=u;Password=p";

            // Act
            var connectionString = DatabaseConnection.Resolve(config, overrideValue);

            // Assert
            Assert.Contains("Host=cli", connectionString, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
        }
    }

    /// <summary>オーバーライドの postgres:// は正規化する。</summary>
    [Fact]
    public void Resolve_ConnectionStringOverride_NormalizesPostgresUrl()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act
        var connectionString = DatabaseConnection.Resolve(
            config,
            "postgres://user:secret@db.example:5433/statevia");

        // Assert
        Assert.Contains("Host=db.example", connectionString, StringComparison.Ordinal);
        Assert.Contains("Password=secret", connectionString, StringComparison.Ordinal);
    }

    /// <summary>null 入力は ArgumentNullException。</summary>
    [Fact]
    public void Resolve_Throws_WhenConfigurationNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DatabaseConnection.Resolve(null!));
    }
}

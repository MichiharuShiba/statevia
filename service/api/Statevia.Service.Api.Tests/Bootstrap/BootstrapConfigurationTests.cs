using Microsoft.Extensions.Configuration;
using Statevia.Service.Api.Bootstrap;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Bootstrap;

/// <summary><see cref="BootstrapConfiguration"/> の検証。</summary>
public sealed class BootstrapConfigurationTests : IDisposable
{
    private readonly string _tempDir;

    public BootstrapConfigurationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"statevia-bootstrap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>--config で指定した JSON から接続文字列を読む。</summary>
    [Fact]
    public void Build_ConfigFile_ExposesConnectionString()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "bootstrap.json");
        File.WriteAllText(
            configPath,
            """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Host=from-file;Database=statevia;Username=u;Password=p"
              }
            }
            """);

        var previousDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            Environment.SetEnvironmentVariable("DATABASE_URL", null);

            // Act
            var configuration = BootstrapConfiguration.Build(
                new BootstrapGlobalCliOptions { ConfigPath = configPath });
            var connectionString = DatabaseConnection.Resolve(configuration);

            // Assert
            Assert.Contains("Host=from-file", connectionString, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
        }
    }

    /// <summary>存在しない --config は例外。</summary>
    [Fact]
    public void Build_MissingConfigFile_Throws()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            BootstrapConfiguration.Build(
                new BootstrapGlobalCliOptions { ConfigPath = Path.Combine(_tempDir, "missing.json") }));
    }
}

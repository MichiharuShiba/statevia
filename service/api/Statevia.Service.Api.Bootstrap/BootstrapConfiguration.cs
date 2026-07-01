using Microsoft.Extensions.Configuration;

namespace Statevia.Service.Api.Bootstrap;

/// <summary>ブートストラップ CLI 用 <see cref="IConfiguration"/> 構築。</summary>
internal static class BootstrapConfiguration
{
    /// <summary>設定ファイル・環境変数から構成を読み込む。</summary>
    public static IConfiguration Build(BootstrapGlobalCliOptions globalOptions)
    {
        ArgumentNullException.ThrowIfNull(globalOptions);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(globalOptions.ConfigPath))
        {
            var configPath = Path.GetFullPath(globalOptions.ConfigPath);
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found: {configPath}", configPath);

            builder.AddJsonFile(configPath, optional: false, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }
}

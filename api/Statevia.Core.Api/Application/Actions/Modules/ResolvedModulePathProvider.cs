using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Statevia.Modules;

namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>解決済み modules ルートを提供する。</summary>
internal interface IResolvedModulePathProvider
{
    /// <summary>絶対パス。</summary>
    string ModulesRoot { get; }
}

/// <summary><see cref="ModulePathResolver"/> を Core-API 設定に接続する。</summary>
internal sealed class ResolvedModulePathProvider : IResolvedModulePathProvider
{
    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="configuration">アプリケーション設定。</param>
    /// <param name="hostEnvironment">content root。</param>
    /// <param name="logger">解決パスログ用。</param>
    public ResolvedModulePathProvider(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<ResolvedModulePathProvider> logger)
    {
        ModulesRoot = ModulePathResolver.Resolve(
            hostEnvironment.ContentRootPath,
            Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable),
            configuration[ModulePathResolver.ConfigurationKey]);
        ResolvedModulePathProviderLog.ModulesRootResolved(logger, ModulesRoot);
    }

    /// <inheritdoc />
    public string ModulesRoot { get; }
}

internal static partial class ResolvedModulePathProviderLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Modules root resolved to {ModulesRoot}")]
    public static partial void ModulesRootResolved(ILogger logger, string modulesRoot);
}

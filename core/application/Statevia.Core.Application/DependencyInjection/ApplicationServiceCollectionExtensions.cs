using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Services;

namespace Statevia.Core.Application.DependencyInjection;

/// <summary>
/// Core.Application ユースケース層の DI 登録。
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Core.Application のユースケースサービスを DI コンテナに登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <returns>チェーン用の <paramref name="services"/>。</returns>
    public static IServiceCollection AddStateviaCoreApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommandDedupService, CommandDedupService>();
        services.AddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();
        services.AddScoped<IDefinitionService, DefinitionService>();
        services.AddScoped<IExecutionSecuritySnapshotFactory, ExecutionSecuritySnapshotFactory>();
        services.AddSingleton<IActionSchemaService, ActionSchemaService>();
        services.AddSingleton<IDefinitionSchemaService, DefinitionSchemaService>();
        services.AddScoped<IExecutionService, ExecutionService>();

        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Statevia.Infrastructure.Security.DependencyInjection;

/// <summary>認証・認可インフラの DI 登録。</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// JWT、テナント文脈、Platform データアクセス、認可ポート実装を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    public static IServiceCollection AddStateviaInfrastructureSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<PasswordCredentialService>();
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        services.AddScoped<Statevia.Core.Application.Contracts.Security.IPrincipalDataAccess, PrincipalDataAccessAdapter>();
        services.AddScoped<IApiKeyAuthenticationService, ApiKeyAuthenticationService>();
        services.AddScoped<ITenantAdminAuthorization, TenantAdminAuthorization>();
        services.AddScoped<IRuntimePermissionAuthorization, RuntimePermissionAuthorization>();
        services.AddScoped<IExecutionMutationAuthorization, ExecutionMutationAuthorization>();

        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName));

        return services;
    }
}

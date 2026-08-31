using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Statevia.Infrastructure.Security.DependencyInjection;

/// <summary>認証・認可インフラの DI 登録。</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// JWT、テナント文脈、Platform データアクセス、認可ポート実装を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    /// <remarks>
    /// Production / Staging では JWT の開発既定 SigningKey と 32 文字未満を起動時に拒否する。失敗メッセージに鍵値は出さない。
    /// </remarks>
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
        services.AddScoped<IPrincipalDataAccess, PrincipalDataAccessAdapter>();
        services.AddScoped<IApiKeyAuthenticationService, ApiKeyAuthenticationService>();
        services.AddScoped<ITenantAdminAuthorization, TenantAdminAuthorization>();
        services.AddScoped<IRuntimePermissionAuthorization, RuntimePermissionAuthorization>();
        services.AddScoped<IModuleManagementAuthorization, ModuleManagementAuthorization>();
        services.AddScoped<IExecutionMutationAuthorization, ExecutionMutationAuthorization>();
        services.AddScoped<TenantAdminBootstrap>();

        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey),
                "Auth:Jwt:SigningKey must not be empty.")
            .Validate(
                o => o.AccessTokenLifetimeMinutes >= 1,
                "Auth:Jwt:AccessTokenLifetimeMinutes must be >= 1.")
            .Validate<IHostEnvironment>(
                static (options, environment) =>
                    !JwtAuthOptions.IsRestrictedHostEnvironment(environment)
                    || JwtAuthOptions.MeetsRestrictedEnvironmentSigningKeyPolicy(options.SigningKey),
                JwtAuthOptions.RestrictedEnvironmentSigningKeyMessage)
            .ValidateOnStart();

        return services;
    }
}

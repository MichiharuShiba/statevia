using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Statevia.Infrastructure.Modules.DependencyInjection;

/// <summary>Action Module ホスト・Source・署名検証の DI 登録。</summary>
public static class ModulesServiceCollectionExtensions
{
    /// <summary>
    /// Module Source 集約、<see cref="ModuleHost"/>、署名検証を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    public static IServiceCollection AddStateviaInfrastructureModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ModuleHostOptions>()
            .Bind(configuration.GetSection(ModuleHostOptions.SectionName));
        services.AddOptions<ModuleSigningOptions>()
            .Bind(configuration.GetSection(ModuleSigningOptions.SectionName));
        services.AddSingleton<IResolvedModulePathProvider, ResolvedModulePathProvider>();
        // 各 Source を IModuleSource として登録し、CompositeModuleSource が Priority 昇順で集約する。
        // Composite は concrete 登録（IModuleSource では登録しない）とし、自身が IEnumerable<IModuleSource>
        // へ含まれることによる自己参照・解決時の無限再帰を避ける。
        services.AddSingleton<IModuleSource, FilesystemModuleSource>();
        services.AddOptions<OciModuleSourceOptions>()
            .Bind(configuration.GetSection(OciModuleSourceOptions.SectionName));
        if (configuration.GetValue<bool>($"{OciModuleSourceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient(OrasOciArtifactFetcher.HttpClientName);
            services.AddSingleton<IOciArtifactFetcher, OrasOciArtifactFetcher>();
            services.AddSingleton<IModuleSource, OciModuleSource>();
        }

        services.AddOptions<S3ModuleSourceOptions>()
            .Bind(configuration.GetSection(S3ModuleSourceOptions.SectionName));
        if (configuration.GetValue<bool>($"{S3ModuleSourceOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<IS3ArtifactFetcher, AwsS3ArtifactFetcher>();
            services.AddSingleton<IModuleSource, S3ModuleSource>();
        }

        services.AddSingleton<CompositeModuleSource>();
        services.AddSingleton<ModuleLoadCatalog>();
        services.AddSingleton<IModuleSignatureVerifier, ModuleSignatureVerifier>();
        services.AddSingleton<ModuleHost>(sp =>
            ActivatorUtilities.CreateInstance<ModuleHost>(sp, sp.GetRequiredService<CompositeModuleSource>()));

        return services;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Actions.Abstractions.Visibility;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Execution;
using Statevia.Core.Api.Application.Actions.Modules;
using Statevia.Core.Api.Application.Actions.Infrastructure;
using Statevia.Core.Api.Application.Actions.Visibility;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.DependencyInjection;
using Statevia.Core.Engine.Infrastructure;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// Core-API の DI・MVC 登録。
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// DbContext、ワークフローエンジン、ドメインサービス、HTTP ログ設定を登録する。
    /// </summary>
    public static IServiceCollection AddStateviaCoreApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = DatabaseConnection.Resolve(configuration);
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<ITenantQueryFilterOptions>(EnabledTenantQueryFilterOptions.Instance);
        services.AddDbContextFactory<CoreDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<PasswordCredentialService>();
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        services.AddScoped<IApiKeyAuthenticationService, ApiKeyAuthenticationService>();
        services.AddScoped<ITenantAdminAuthorization, TenantAdminAuthorization>();
        services.AddScoped<IRuntimePermissionAuthorization, RuntimePermissionAuthorization>();
        services.AddScoped<IExecutionMutationAuthorization, ExecutionMutationAuthorization>();
        services.AddScoped<IExecutionSecuritySnapshotFactory, ExecutionSecuritySnapshotFactory>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
        services.AddHostedService<TenantBootstrapHostedService>();
        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName));

        services.AddScoped<ICoreUnitOfWorkFactory, CoreUnitOfWorkFactory>();
        services.AddScoped<ICoreTransactionExecutor, CoreTransactionExecutor>();
        services.AddScoped<IExecutionMutationPersistence, ExecutionMutationPersistence>();

        services.AddScoped<DisplayIdServiceImpl>();
        services.AddScoped<IDisplayIdService>(sp => sp.GetRequiredService<DisplayIdServiceImpl>());
        services.AddScoped<IDisplayIdWriteService>(sp => sp.GetRequiredService<DisplayIdServiceImpl>());
        services.AddScoped<IExecutionReadModelService, ExecutionReadModelService>();
        services.AddSingleton<IIdGenerator, UuidV7Generator>();

        services.AddSingleton<IExecutionIdGenerator>(
            sp => new DelegateExecutionIdGenerator(() => sp.GetRequiredService<IIdGenerator>().NewGuid().ToString()));
        services.AddStateviaExecutionEngine();
        services.AddScoped<ICommandDedupService, CommandDedupService>();
        services.AddScoped<IDefinitionRepository, DefinitionRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IExecutionCursorRepository, ExecutionCursorRepository>();
        services.AddScoped<IExecutionWaitRepository, ExecutionWaitRepository>();
        services.AddScoped<ICommandDedupRepository, CommandDedupRepository>();
        services.AddScoped<IEventDeliveryDedupRepository, EventDeliveryDedupRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IDefinitionService, DefinitionService>();
        services.AddSingleton<IDefinitionSchemaService, DefinitionSchemaService>();
        services.AddSingleton<IActionSchemaService, ActionSchemaService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddOptions<ExecutionProjectionQueueOptions>()
            .Bind(configuration.GetSection("ExecutionProjectionQueue"))
            .Validate(o => o.MaxGlobalQueueSize >= 1, "ExecutionProjectionQueue:MaxGlobalQueueSize must be >= 1.")
            .Validate(o => o.ProjectionFlushDebounceMs is >= 0 and <= 250, "ExecutionProjectionQueue:ProjectionFlushDebounceMs must be between 0 and 250.")
            .Validate(o => o.MaxRetryAttempts is >= 1 and <= 100, "ExecutionProjectionQueue:MaxRetryAttempts must be between 1 and 100.")
            .Validate(o => o.RetryBaseDelayMs is >= 0 and <= 60_000, "ExecutionProjectionQueue:RetryBaseDelayMs must be between 0 and 60000.")
            .Validate(o => o.RetryMaxDelayMs is >= 0 and <= 600_000, "ExecutionProjectionQueue:RetryMaxDelayMs must be between 0 and 600000.")
            .Validate(o => o.RetryMaxDelayMs >= o.RetryBaseDelayMs, "ExecutionProjectionQueue:RetryMaxDelayMs must be >= RetryBaseDelayMs.");
        services.AddSingleton<ExecutionProjectionUpdateQueueService>();
        services.AddSingleton<IExecutionProjectionUpdateQueue>(sp => sp.GetRequiredService<ExecutionProjectionUpdateQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<ExecutionProjectionUpdateQueueService>());
        services.AddScoped<ExecutionStreamService>();
        services.AddScoped<IGraphDefinitionService, GraphDefinitionService>();
        services.AddHttpClient();
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .PostConfigure(ApplyNotificationOptionsEnvironmentOverrides);
        services.AddSingleton<EnvironmentSmtpConnectionSettingsProvider>();
        services.AddSingleton<DatabaseSmtpConnectionSettingsProvider>();
        services.AddSingleton<KmsSmtpConnectionSettingsProvider>();
        services.AddSingleton<SmtpConnectionSettingsProviderFactory>();
        services.AddSingleton<ISmtpConnectionSettingsProvider>(sp =>
            sp.GetRequiredService<SmtpConnectionSettingsProviderFactory>());
        services.AddSingleton<DevelopmentNotificationSender>();
        services.AddSingleton<SmtpNotificationSender>();
        services.AddSingleton<NotificationSenderResolver>();
        services.AddScoped<IChildWorkflowRunner, ChildWorkflowRunner>();
        services.AddSingleton<InMemoryActionCatalog>(sp =>
        {
            var catalog = new InMemoryActionCatalog();
            DefinitionCompilerService.RegisterBuiltinActions(catalog);
            return catalog;
        });
        services.AddSingleton<IActionCatalog>(sp => sp.GetRequiredService<InMemoryActionCatalog>());
        services.AddOptions<ModuleHostOptions>()
            .Bind(configuration.GetSection(ModuleHostOptions.SectionName));
        services.AddOptions<ModuleSigningOptions>()
            .Bind(configuration.GetSection(ModuleSigningOptions.SectionName));
        services.AddSingleton<IResolvedModulePathProvider, ResolvedModulePathProvider>();
        // 各 Source を IModuleSource として登録し、CompositeModuleSource が Priority 昇順で集約する。
        // Composite は concrete 登録（IModuleSource では登録しない）とし、自身が IEnumerable<IModuleSource>
        // へ含まれることによる自己参照・解決時の無限再帰を避ける。
        services.AddSingleton<IModuleSource, FilesystemModuleSource>();
        // OCI Source は明示有効化時のみ登録する（未設定なら filesystem のみ＝後方互換）。
        services.AddOptions<OciModuleSourceOptions>()
            .Bind(configuration.GetSection(OciModuleSourceOptions.SectionName));
        if (configuration.GetValue<bool>($"{OciModuleSourceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient(OrasOciArtifactFetcher.HttpClientName);
            services.AddSingleton<IOciArtifactFetcher, OrasOciArtifactFetcher>();
            services.AddSingleton<IModuleSource, OciModuleSource>();
        }
        services.AddSingleton<CompositeModuleSource>();
        services.AddSingleton<ModuleLoadCatalog>();
        services.AddSingleton<IModuleSignatureVerifier, ModuleSignatureVerifier>();
        // ModuleHost は単一 Source として CompositeModuleSource を consume する。
        services.AddSingleton<ModuleHost>(sp =>
            ActivatorUtilities.CreateInstance<ModuleHost>(sp, sp.GetRequiredService<CompositeModuleSource>()));
        services.AddSingleton<IModuleManagementService, ModuleManagementService>();
        services.AddSingleton<ModuleLoadHostedServiceDependencies>();
        services.AddHostedService<ModuleLoadHostedService>();
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        services.AddOptions<ExecutionPolicyOptions>()
            .Bind(configuration.GetSection(ExecutionPolicyOptions.SectionName));
        services.AddOptions<ActionHostClientOptions>()
            .Bind(configuration.GetSection(ActionHostClientOptions.SectionName));
        services.AddSingleton<IExecutionPolicyProvider, TenantExecutionPolicyProvider>();
        services.AddSingleton<IActionExecutionPolicy, ConfigurableExecutionPolicy>();
        services.AddSingleton<GrpcActionHostExecutionClient>();
        services.AddSingleton<IActionHostExecutionClient>(sp => sp.GetRequiredService<GrpcActionHostExecutionClient>());
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, ContainerActionBackend>();
        services.AddSingleton<IActionExecutionBackend, WasmActionBackend>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();
        services.AddSingleton<StateWorkflowDefinitionLoader>();
        services.AddSingleton<NodesWorkflowDefinitionLoader>();
        services.AddSingleton<IDefinitionLoadStrategy, DefinitionLoadStrategy>();
        services.AddSingleton<IDefinitionCompilerService, DefinitionCompilerService>();

        services.AddOptions<RequestLogOptions>()
            .Configure<IHostEnvironment>(ConfigureRequestLogOptions);

        services.AddOptions<EventDeliveryRetryOptions>()
            .Bind(configuration.GetSection("EventDelivery:Retry"))
            .Validate(o => o.MaxAttempts is >= 1 and <= 50, "EventDelivery:Retry:MaxAttempts must be between 1 and 50.")
            .Validate(o => o.BaseDelayMs is >= 0 and <= 600_000, "EventDelivery:Retry:BaseDelayMs is out of range.")
            .Validate(o => o.MaxDelayMs is >= 0 and <= 600_000, "EventDelivery:Retry:MaxDelayMs is out of range.")
            .Validate(o => o.MaxTotalBackoffMs is >= 0 and <= 600_000, "EventDelivery:Retry:MaxTotalBackoffMs is out of range.")
            .Validate(
                o => o.SerializablePersistenceMaxAttempts is >= 1 and <= 50,
                "EventDelivery:Retry:SerializablePersistenceMaxAttempts must be between 1 and 50.");

        services.AddHttpContextAccessor();
        services.AddCors();
        services.AddStateviaOpenApi();
        services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
            .AddControllersAsServices()
            .AddJsonOptions(jsonOptions =>
            {
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                jsonOptions.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            });

        services.Configure<ApiBehaviorOptions>(ConfigureApiValidationResponse);

        return services;
    }

    private static void ConfigureRequestLogOptions(RequestLogOptions options, IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            options.LogRequestBody = false;
            options.LogResponseBody = false;
        }
        else
        {
            options.LogRequestBody = true;
            options.LogResponseBody = true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("STATEVIA_LOG_HTTP_BODIES"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            options.LogRequestBody = true;
            options.LogResponseBody = true;
        }
    }

    private static void ApplyNotificationOptionsEnvironmentOverrides(NotificationOptions options)
    {
        var source = Environment.GetEnvironmentVariable(NotificationOptions.SmtpSourceEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (string.Equals(source, "Kms", StringComparison.OrdinalIgnoreCase))
        {
            options.SmtpSettingsSource = NotificationSmtpSettingsSource.KeyManagementService;
            return;
        }

        if (Enum.TryParse(source, ignoreCase: true, out NotificationSmtpSettingsSource parsed))
        {
            options.SmtpSettingsSource = parsed;
        }
    }

    private static void ConfigureApiValidationResponse(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(error => error.ErrorMessage))
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();

            var message = errors.Length > 0 ? string.Join("; ", errors) : "Validation failed";

            var details = context.ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(error => error.ErrorMessage).ToArray() ?? Array.Empty<string>());

            return new UnprocessableEntityObjectResult(
                new ErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = message,
                        Details = details
                    }
                });
        };
    }
}

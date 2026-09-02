using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Application.Actions.Infrastructure;
using Statevia.Infrastructure.Modules;
using Statevia.Infrastructure.Modules.DependencyInjection;
using Statevia.Service.Api.Application.Actions.Modules;
using Statevia.Service.Api.Application.Actions.Visibility;
using Statevia.Service.Api.Application.Definition;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Contracts;
using Statevia.Infrastructure.Common;
using Statevia.Infrastructure.Common.DependencyInjection;
using Statevia.Infrastructure.Notification.DependencyInjection;
using Statevia.Infrastructure.Persistence.DependencyInjection;
using Statevia.Infrastructure.Security.DependencyInjection;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Persistence;
using Statevia.Core.Application.DependencyInjection;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.DependencyInjection;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Runtime.DependencyInjection;
using Statevia.Runtime.Configuration;
using Statevia.Runtime.Services;
using Statevia.Service.Api.Services;
namespace Statevia.Service.Api.Hosting;

/// <summary>
/// Service API / Worker の DI 登録。
/// </summary>
/// <remarks>Worker ホストから <see cref="AddStateviaWorkerHost"/> を参照するため public。</remarks>
#pragma warning disable CA1515 // Worker ホストが参照するため public を維持する
public static class ServiceCollectionExtensions
#pragma warning restore CA1515
{
    /// <summary>
    /// Service API 向けに実行ランタイムと HTTP アダプタを登録する。
    /// </summary>
    public static IServiceCollection AddStateviaServiceApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddStateviaExecutionRuntime(configuration);
        services.AddStateviaApiHost(configuration);
        return services;
    }
    /// <summary>
    /// Worker 専用ホスト向けに実行ランタイムのみを登録する（HTTP アダプタなし）。
    /// </summary>
    /// <remarks>
    /// プロセス内 Scheduler / OwnershipRecovery は Off、Worker HostedService は On に固定する。
    /// </remarks>
    public static IServiceCollection AddStateviaWorkerHost(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        // 専用 Worker では API 内スキャナを二重起動しない（後勝ちの in-memory で上書き）。
        var workerConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RuntimeOptions.SectionName}:EnableInProcessWorker"] = "true",
                [$"{RuntimeOptions.SectionName}:EnableInProcessDelayWaitScheduler"] = "false",
                [$"{RuntimeOptions.SectionName}:EnableInProcessOwnershipRecovery"] = "false"
            })
            .Build();
        return services.AddStateviaExecutionRuntime(workerConfiguration);
    }
    /// <summary>
    /// Persistence / Engine / Actions / Runtime HostedService など実行系 DI を登録する。
    /// </summary>
    /// <remarks>HTTP コントローラ・CORS・OpenAPI・テナント管理 API 向けサービスは含まない。</remarks>
    public static IServiceCollection AddStateviaExecutionRuntime(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = DatabaseConnection.Resolve(configuration);
        services.AddSingleton<ITenantQueryFilterOptions>(EnabledTenantQueryFilterOptions.Instance);
        services.AddStateviaInfrastructurePersistence(connectionString);
        services.AddStateviaInfrastructureSecurity(configuration);
        services.AddStateviaRuntimeOptions(configuration);
        services.AddSingleton<INodesSchemaProvider, Application.Definition.NodesSchemaProvider>();
        services.AddStateviaCoreApplication();
        services.AddScoped<IExecutionMutationPersistence, ExecutionMutationPersistence>();
        services.AddScoped<DisplayIdServiceImpl>();
        services.AddScoped<IDisplayIdService>(sp => sp.GetRequiredService<DisplayIdServiceImpl>());
        services.AddScoped<IDisplayIdWriteService>(sp => sp.GetRequiredService<DisplayIdServiceImpl>());
        services.AddScoped<IExecutionReadModelService, ExecutionReadModelService>();
        services.AddStateviaInfrastructureCommon();
        services.AddSingleton<IExecutionIdGenerator>(
            sp => new DelegateExecutionIdGenerator(() => sp.GetRequiredService<IIdGenerator>().NewSequentialGuid().ToString()));
        services.AddStateviaExecutionEngine();
        AddExecutionProjectionQueueOptions(services, configuration);
        services.AddSingleton<ExecutionProjectionUpdateQueueService>();
        services.AddSingleton<IExecutionProjectionUpdateQueue>(sp => sp.GetRequiredService<ExecutionProjectionUpdateQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<ExecutionProjectionUpdateQueueService>());
        var runtimeOptions = configuration.GetSection(RuntimeOptions.SectionName).Get<RuntimeOptions>()
            ?? new RuntimeOptions();
        if (runtimeOptions.EnableInProcessWorker)
            services.AddStateviaRuntimeWorkerHostedService();
        if (runtimeOptions.EnableInProcessDelayWaitScheduler)
            services.AddHostedService<DelayWaitSchedulerHostedService>();
        if (runtimeOptions.EnableInProcessOwnershipRecovery)
            services.AddHostedService<ExecutionOwnershipRecoveryHostedService>();
        services.AddScoped<IGraphDefinitionService, GraphDefinitionService>();
        services.AddHttpClient();
        services.AddStateviaInfrastructureNotification(configuration);
        services.AddScoped<IChildWorkflowRunner, ChildWorkflowRunner>();
        services.AddSingleton<InMemoryActionCatalog>(sp =>
        {
            var catalog = new InMemoryActionCatalog();
            DefinitionCompilerService.RegisterBuiltinActions(catalog);
            return catalog;
        });
        services.AddSingleton<IActionCatalog>(sp => sp.GetRequiredService<InMemoryActionCatalog>());
        services.AddStateviaInfrastructureModules(configuration);
        services.AddSingleton<IModuleManagementService, ModuleManagementService>();
        services.AddSingleton<ModuleLoadHostedServiceDependencies>();
        services.AddHostedService<ModuleLoadHostedService>();
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        AddExecutionPolicyOptions(services, configuration);
        AddActionHostClientOptions(services, configuration);
        services.AddSingleton<IExecutionPolicyProvider, TenantExecutionPolicyProvider>();
        services.AddSingleton<IActionExecutionPolicy, ConfigurableExecutionPolicy>();
        services.AddSingleton<GrpcActionHostExecutionClient>();
        services.AddSingleton<IActionHostExecutionClient>(sp => sp.GetRequiredService<GrpcActionHostExecutionClient>());
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, ContainerActionBackend>();
        services.AddSingleton<IActionExecutionBackend, WasmActionBackend>();
        services.AddSingleton<IDockerContainerClient, DockerDotNetContainerClient>();
        services.AddSingleton<IEphemeralActionHostExecutor, GrpcEphemeralActionHostExecutor>();
        services.AddSingleton<IActionSandboxRuntime, DockerSandboxRuntime>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();
        services.AddSingleton<StateWorkflowDefinitionLoader>();
        services.AddSingleton<NodesWorkflowDefinitionLoader>();
        services.AddSingleton<IDefinitionLoadStrategy, DefinitionLoadStrategy>();
        services.AddSingleton<IDefinitionCompilerService, DefinitionCompilerService>();
        AddEventDeliveryRetryOptions(services, configuration);
        AddForkChildExpansionOptions(services, configuration);
        // Worker でも解決可能にする（HTTP 文脈がなければ空文字）。
        services.AddHttpContextAccessor();
        services.AddScoped<Statevia.Core.Application.Contracts.Services.ICorrelationIdAccessor, Infrastructure.HttpContextCorrelationIdAccessor>();
        return services;
    }
    /// <summary>
    /// HTTP API 向けサービス（認証・テナント管理・MVC・OpenAPI 等）を登録する。
    /// </summary>
    public static IServiceCollection AddStateviaApiHost(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoginFailureLockStore, InMemoryLoginFailureLockStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
        services.AddOptions<DevAdminBootstrapOptions>()
            .Bind(configuration.GetSection(DevAdminBootstrapOptions.SectionName));
        services.AddHostedService<TenantBootstrapHostedService>();
        services.AddScoped<ExecutionStreamService>();
        services.AddOptions<RequestLogOptions>()
            .Configure<IHostEnvironment>(ConfigureRequestLogOptions);
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));
        AddCorsDefaultPolicy(services, configuration);
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
    /// <summary>Projection キュー Options のバインドと起動時検証（分類 A）。</summary>
    private static void AddExecutionProjectionQueueOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutionProjectionQueueOptions>()
            .Bind(configuration.GetSection("ExecutionProjectionQueue"))
            .Validate(o => o.MaxGlobalQueueSize >= 1, "ExecutionProjectionQueue:MaxGlobalQueueSize must be >= 1.")
            .Validate(o => o.ProjectionFlushDebounceMs is >= 0 and <= 250, "ExecutionProjectionQueue:ProjectionFlushDebounceMs must be between 0 and 250.")
            .Validate(o => o.MaxRetryAttempts is >= 1 and <= 100, "ExecutionProjectionQueue:MaxRetryAttempts must be between 1 and 100.")
            .Validate(o => o.RetryBaseDelayMs is >= 0 and <= 60_000, "ExecutionProjectionQueue:RetryBaseDelayMs must be between 0 and 60000.")
            .Validate(o => o.RetryMaxDelayMs is >= 0 and <= 600_000, "ExecutionProjectionQueue:RetryMaxDelayMs must be between 0 and 600000.")
            .Validate(o => o.RetryMaxDelayMs >= o.RetryBaseDelayMs, "ExecutionProjectionQueue:RetryMaxDelayMs must be >= RetryBaseDelayMs.")
            .ValidateOnStart();
    }
    /// <summary>Execution Policy / Docker サンドボックス Options のバインドと起動時検証（分類 A）。</summary>
    private static void AddExecutionPolicyOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutionPolicyOptions>()
            .Bind(configuration.GetSection(ExecutionPolicyOptions.SectionName))
            .Validate(
                IsValidSandboxTimeoutSeconds,
                $"Statevia:ExecutionPolicy:Sandbox:TimeoutSeconds must be between {SandboxOptions.MinTimeoutSeconds} and {SandboxOptions.MaxTimeoutSeconds} when set.")
            .Validate(
                IsValidSandboxMemoryLimitMiB,
                $"Statevia:ExecutionPolicy:Sandbox:MemoryLimitMiB must be between {SandboxOptions.MinMemoryLimitMiB} and {SandboxOptions.MaxMemoryLimitMiB} when set.")
            .Validate(
                IsValidSandboxCpuLimit,
                $"Statevia:ExecutionPolicy:Sandbox:CpuLimit must be between {SandboxOptions.MinCpuLimit} and {SandboxOptions.MaxCpuLimit} when set.")
            .Validate(
                IsValidDockerDefaultTimeoutSeconds,
                $"Statevia:ExecutionPolicy:Sandbox:Docker:DefaultTimeoutSeconds must be between {DockerSandboxOptions.MinDefaultTimeoutSeconds} and {DockerSandboxOptions.MaxDefaultTimeoutSeconds}.")
            .Validate(
                IsValidDockerGrpcPort,
                $"Statevia:ExecutionPolicy:Sandbox:Docker:GrpcPort must be between {DockerSandboxOptions.MinGrpcPort} and {DockerSandboxOptions.MaxGrpcPort}.")
            .Validate(
                IsSupportedDockerNetworkMode,
                "Statevia:ExecutionPolicy:Sandbox:Docker:NetworkMode 'none' is not supported.")
            .ValidateOnStart();
    }
    /// <summary>Action Host クライアント Options のバインドと起動時検証（分類 A / C）。</summary>
    private static void AddActionHostClientOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ActionHostClientOptions>()
            .Bind(configuration.GetSection(ActionHostClientOptions.SectionName))
            .Validate(
                IsValidActionHostBaseUrl,
                "Statevia:ActionHost:BaseUrl must be an absolute http(s) URI when set.")
            .ValidateOnStart();
    }
    /// <summary>イベント配送リトライ Options のバインドと起動時検証（分類 A）。</summary>
    private static void AddEventDeliveryRetryOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Statevia.Core.Application.Configuration.EventDeliveryRetryOptions>()
            .Bind(configuration.GetSection("EventDelivery:Retry"))
            .Validate(o => o.MaxAttempts is >= 1 and <= 50, "EventDelivery:Retry:MaxAttempts must be between 1 and 50.")
            .Validate(o => o.BaseDelayMs is >= 0 and <= 600_000, "EventDelivery:Retry:BaseDelayMs is out of range.")
            .Validate(o => o.MaxDelayMs is >= 0 and <= 600_000, "EventDelivery:Retry:MaxDelayMs is out of range.")
            .Validate(o => o.MaxDelayMs >= o.BaseDelayMs, "EventDelivery:Retry:MaxDelayMs must be >= BaseDelayMs.")
            .Validate(o => o.MaxTotalBackoffMs is >= 0 and <= 600_000, "EventDelivery:Retry:MaxTotalBackoffMs is out of range.")
            .Validate(
                o => o.SerializablePersistenceMaxAttempts is >= 1 and <= 50,
                "EventDelivery:Retry:SerializablePersistenceMaxAttempts must be between 1 and 50.")
            .ValidateOnStart();
    }

    /// <summary>Fork 物理子展開リトライ Options。</summary>
    private static void AddForkChildExpansionOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForkChildExpansionOptions>()
            .Bind(configuration.GetSection(ForkChildExpansionOptions.SectionName))
            .Validate(o => o.MaxAttempts is >= 1 and <= 50, $"{ForkChildExpansionOptions.SectionName}:MaxAttempts must be between 1 and 50.")
            .Validate(o => o.BaseDelayMs is >= 0 and <= 600_000, $"{ForkChildExpansionOptions.SectionName}:BaseDelayMs is out of range.")
            .ValidateOnStart();
    }
    private static bool IsValidSandboxTimeoutSeconds(ExecutionPolicyOptions options) =>
        options.Sandbox.TimeoutSeconds is null
        || options.Sandbox.TimeoutSeconds is >= SandboxOptions.MinTimeoutSeconds
            and <= SandboxOptions.MaxTimeoutSeconds;
    private static bool IsValidSandboxMemoryLimitMiB(ExecutionPolicyOptions options) =>
        options.Sandbox.MemoryLimitMiB is null
        || options.Sandbox.MemoryLimitMiB is >= SandboxOptions.MinMemoryLimitMiB
            and <= SandboxOptions.MaxMemoryLimitMiB;
    private static bool IsValidSandboxCpuLimit(ExecutionPolicyOptions options) =>
        options.Sandbox.CpuLimit is null
        || options.Sandbox.CpuLimit is >= SandboxOptions.MinCpuLimit
            and <= SandboxOptions.MaxCpuLimit;
    private static bool IsValidDockerDefaultTimeoutSeconds(ExecutionPolicyOptions options) =>
        options.Sandbox.Docker.DefaultTimeoutSeconds is >= DockerSandboxOptions.MinDefaultTimeoutSeconds
            and <= DockerSandboxOptions.MaxDefaultTimeoutSeconds;
    private static bool IsValidDockerGrpcPort(ExecutionPolicyOptions options) =>
        options.Sandbox.Docker.GrpcPort is >= DockerSandboxOptions.MinGrpcPort
            and <= DockerSandboxOptions.MaxGrpcPort;
    private static bool IsSupportedDockerNetworkMode(ExecutionPolicyOptions options) =>
        string.IsNullOrWhiteSpace(options.Sandbox.Docker.NetworkMode)
        || !string.Equals(options.Sandbox.Docker.NetworkMode.Trim(), "none", StringComparison.OrdinalIgnoreCase);
    private static bool IsValidActionHostBaseUrl(ActionHostClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return true;
        }
        return Uri.TryCreate(options.BaseUrl.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
    /// <summary>
    /// 設定されたオリジンだけを許可する既定 CORS ポリシーを登録する。未設定時は ACAO を付けない。
    /// </summary>
    /// <param name="services">DI コンテナ。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    private static void AddCorsDefaultPolicy(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = ResolveCorsAllowedOrigins(configuration);
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    /// <summary>空白・末尾スラッシュを除いた許可オリジン一覧を返す。</summary>
    private static string[] ResolveCorsAllowedOrigins(IConfiguration configuration)
    {
        var configured = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins
            ?? [];
        return configured
            .Where(static origin => !string.IsNullOrWhiteSpace(origin))
            .Select(static origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
    private static void ConfigureApiValidationResponse(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var detailItems = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(error => new
                {
                    field = NormalizeModelStateFieldName(kvp.Key),
                    message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Validation failed"
                        : error.ErrorMessage
                }))
                .ToArray();
            var message = detailItems.Length > 0
                ? string.Join("; ", detailItems.Select(d => d.message))
                : "Validation failed";
            return new UnprocessableEntityObjectResult(
                new ErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = message,
                        Details = detailItems
                    }
                });
        };
    }
    /// <summary>
    /// ModelState キー（例: <c>request.Name</c>）を camelCase のフィールド名へ正規化する。
    /// </summary>
    private static string NormalizeModelStateFieldName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;
        var segment = key;
        var lastDot = key.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < key.Length - 1)
            segment = key[(lastDot + 1)..];
        if (segment.Length == 0)
            return key;
        if (segment.Length == 1)
            return char.ToLowerInvariant(segment[0]).ToString();
        return string.Create(segment.Length, segment, static (span, value) =>
        {
            span[0] = char.ToLowerInvariant(value[0]);
            value.AsSpan(1).CopyTo(span[1..]);
        });
    }
}

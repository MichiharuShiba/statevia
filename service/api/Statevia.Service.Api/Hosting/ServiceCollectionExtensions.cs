using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Services;
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
using Statevia.Service.Api.Infrastructure;
using Statevia.Infrastructure.Notification.DependencyInjection;
using Statevia.Infrastructure.Persistence.DependencyInjection;
using Statevia.Infrastructure.Security.DependencyInjection;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Persistence.Repositories;
using Statevia.Service.Api.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.DependencyInjection;
using Statevia.Core.Engine.Infrastructure;

namespace Statevia.Service.Api.Hosting;

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
        services.AddSingleton<ITenantQueryFilterOptions>(EnabledTenantQueryFilterOptions.Instance);
        services.AddStateviaInfrastructurePersistence(connectionString);
        services.AddStateviaInfrastructureSecurity(configuration);

        services.AddScoped<IExecutionSecuritySnapshotFactory, ExecutionSecuritySnapshotFactory>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
        services.AddHostedService<TenantBootstrapHostedService>();

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
        services.AddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();
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

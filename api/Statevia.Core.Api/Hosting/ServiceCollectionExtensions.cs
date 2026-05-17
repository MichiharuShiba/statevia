using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure;
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
        services.AddDbContextFactory<CoreDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<IDisplayIdService, DisplayIdServiceImpl>();
        services.AddScoped<IExecutionReadModelService, ExecutionReadModelService>();
        services.AddSingleton<IIdGenerator, UuidV7Generator>();

        services.AddSingleton<IWorkflowInstanceIdGenerator>(
            sp => new DelegateWorkflowInstanceIdGenerator(() => sp.GetRequiredService<IIdGenerator>().NewGuid().ToString()));
        services.AddStateviaWorkflowEngine();
        services.AddScoped<ICommandDedupService, CommandDedupService>();
        services.AddScoped<IDefinitionRepository, DefinitionRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<ICommandDedupRepository, CommandDedupRepository>();
        services.AddScoped<IEventDeliveryDedupRepository, EventDeliveryDedupRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IDefinitionService, DefinitionService>();
        services.AddSingleton<IDefinitionSchemaService, DefinitionSchemaService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddOptions<WorkflowProjectionQueueOptions>()
            .Bind(configuration.GetSection("WorkflowProjectionQueue"))
            .Validate(o => o.MaxGlobalQueueSize >= 1, "WorkflowProjectionQueue:MaxGlobalQueueSize must be >= 1.")
            .Validate(o => o.ProjectionFlushDebounceMs is >= 0 and <= 250, "WorkflowProjectionQueue:ProjectionFlushDebounceMs must be between 0 and 250.")
            .Validate(o => o.MaxRetryAttempts is >= 1 and <= 100, "WorkflowProjectionQueue:MaxRetryAttempts must be between 1 and 100.")
            .Validate(o => o.RetryBaseDelayMs is >= 0 and <= 60_000, "WorkflowProjectionQueue:RetryBaseDelayMs must be between 0 and 60000.")
            .Validate(o => o.RetryMaxDelayMs is >= 0 and <= 600_000, "WorkflowProjectionQueue:RetryMaxDelayMs must be between 0 and 600000.")
            .Validate(o => o.RetryMaxDelayMs >= o.RetryBaseDelayMs, "WorkflowProjectionQueue:RetryMaxDelayMs must be >= RetryBaseDelayMs.");
        services.AddSingleton<WorkflowProjectionUpdateQueueService>();
        services.AddSingleton<IWorkflowProjectionUpdateQueue>(sp => sp.GetRequiredService<WorkflowProjectionUpdateQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<WorkflowProjectionUpdateQueueService>());
        services.AddScoped<WorkflowStreamService>();
        services.AddScoped<IGraphDefinitionService, GraphDefinitionService>();
        services.AddSingleton<IActionRegistry>(_ =>
        {
            var registry = new InMemoryActionRegistry();
            DefinitionCompilerService.RegisterBuiltinActions(registry);
            return registry;
        });
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

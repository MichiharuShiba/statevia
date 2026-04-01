using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Engine.Definition;

var builder = WebApplication.CreateBuilder(args);

// DbContext（EF Core + PostgreSQL）
// Docker / CI では DATABASE_URL を優先して接続先を差し替える。
// ただし Npgsql は `Host=...;Database=...` 形式を期待するため、postgres:// 形式は正規化する。
static string NormalizeDatabaseUrl(string url)
{
    if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(url);
        var dbName = uri.AbsolutePath.TrimStart('/');

        var user = "";
        var pass = "";
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts[0]);
            pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        }

        var port = uri.IsDefaultPort ? 5432 : uri.Port;
        return $"Host={uri.Host};Port={port};Database={dbName};Username={user};Password={pass}";
    }

    return url;
}

var rawDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var conn =
    (string.IsNullOrWhiteSpace(rawDatabaseUrl) ? null : NormalizeDatabaseUrl(rawDatabaseUrl))
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=statevia;Username=statevia;Password=statevia";
builder.Services.AddDbContextFactory<CoreDbContext>(options =>
    options.UseNpgsql(conn, o => o.MigrationsHistoryTable("__ef_migrations_history")));

builder.Services.AddScoped<IDisplayIdService, DisplayIdServiceImpl>();
builder.Services.AddScoped<IExecutionReadModelService, ExecutionReadModelService>();
builder.Services.AddSingleton<IIdGenerator, UuidV7Generator>();

// IWorkflowEngine を DI でシングルトン登録（計画 3.3）
// workflowId 未指定時の生成は Core-API の IIdGenerator と同一経路（UUID v7）に揃える。
builder.Services.AddSingleton<IWorkflowEngine>(sp =>
{
    var idGen = sp.GetRequiredService<IIdGenerator>();
    return new WorkflowEngine(new WorkflowEngineOptions
    {
        WorkflowInstanceIdGenerator = new DelegateWorkflowInstanceIdGenerator(() => idGen.NewGuid().ToString())
    });
});
builder.Services.AddScoped<ICommandDedupService, CommandDedupService>();
builder.Services.AddScoped<IDefinitionRepository, DefinitionRepository>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddScoped<ICommandDedupRepository, CommandDedupRepository>();
builder.Services.AddScoped<IEventStoreRepository, EventStoreRepository>();
builder.Services.AddScoped<IDefinitionService, DefinitionService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<WorkflowStreamService>();
builder.Services.AddScoped<IGraphDefinitionService, GraphDefinitionService>();
builder.Services.AddSingleton<IActionRegistry>(_ =>
{
    var registry = new InMemoryActionRegistry();
    DefinitionCompilerService.RegisterBuiltinActions(registry);
    return registry;
});
builder.Services.AddSingleton<StateWorkflowDefinitionLoader>();
builder.Services.AddSingleton<NodesWorkflowDefinitionLoader>();
builder.Services.AddSingleton<IDefinitionLoadStrategy, DefinitionLoadStrategy>();
builder.Services.AddSingleton<IDefinitionCompilerService, DefinitionCompilerService>();
builder.Services.AddCors();
builder.Services.AddControllers(options =>
    {
        // 例外 -> 契約エラー（404/422/500）を一箇所に集約する。
        options.Filters.Add<ApiExceptionFilter>();
    })
    .AddControllersAsServices()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// ASP.NET 標準のモデルバリデーション（[Required] 等）を契約の 422 形式に寄せる。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage))
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToArray();

        var message = errors.Length > 0 ? string.Join("; ", errors) : "Validation failed";

        var details = context.ModelState.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
        );

        return new UnprocessableEntityObjectResult(
            new ErrorResponse
            {
                Error = new ApiError
                {
                    Code = "VALIDATION_ERROR",
                    Message = message,
                    Details = details
                }
            }
        );
    };
});

var app = builder.Build();

// Phase 3.2: UI からの跨域アクセスを許可（v2 では認証なし）
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapControllers();
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();


using Microsoft.EntityFrameworkCore;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

// IWorkflowEngine を DI でシングルトン登録（計画 3.3）
builder.Services.AddSingleton<IWorkflowEngine>(_ => new WorkflowEngine());

// DbContext（EF Core + PostgreSQL）
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=statevia;Username=statevia;Password=statevia";
builder.Services.AddDbContextFactory<CoreDbContext>(options =>
    options.UseNpgsql(conn, o => o.MigrationsHistoryTable("__ef_migrations_history")));

builder.Services.AddScoped<IDisplayIdService, DisplayIdServiceImpl>();
builder.Services.AddScoped<IExecutionReadModelService, ExecutionReadModelService>();
builder.Services.AddScoped<ICommandDedupService, CommandDedupService>();
builder.Services.AddScoped<IDefinitionRepository, DefinitionRepository>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddScoped<ICommandDedupRepository, CommandDedupRepository>();
builder.Services.AddScoped<IDefinitionService, DefinitionService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IGraphDefinitionService, GraphDefinitionService>();
builder.Services.AddSingleton<IDefinitionCompilerService, DefinitionCompilerService>();
builder.Services.AddCors();
builder.Services.AddControllers();

var app = builder.Build();

// Phase 3.2: UI からの跨域アクセスを許可（v2 では認証なし）
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapControllers();
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();
